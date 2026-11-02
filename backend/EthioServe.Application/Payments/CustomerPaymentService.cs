using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Payments;

public sealed class CustomerPaymentService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IClock clock,
    INotifier notifier,
    IValidator<SimulatePaymentRequest> validator,
    IEnumerable<IPaymentGateway> gateways) : ICustomerPaymentService
{
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<StartPaymentResult> StartAsync(
        Guid requestId, string? idempotencyKey, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var gateway = PaymentGateways.FirstAvailable(gateways)
            ?? throw new ServiceUnavailableException("Payments are not available right now.");
        var key = PaymentRules.NamespacedKey(customerId, idempotencyKey);

        var result = await db.WithExclusiveLockAsync(
            $"payment-start:{customerId:N}:{requestId:N}",
            () => StartCoreAsync(customerId, requestId, key, gateway, ct),
            ct);

        var payment = await GetAsync(result.PaymentId, ct);
        return new StartPaymentResult(payment, result.Created);
    }

    public async Task<PagedResult<PaymentDto>> ListAsync(PaymentQuery query, CancellationToken ct = default)
    {
        var customerId = CustomerId;

        if (query.RequestId is { } requestId
            && !await db.ServiceRequests.AsNoTracking()
                .AnyAsync(r => r.Id == requestId && r.CustomerId == customerId, ct))
        {
            throw new NotFoundException("Request not found.");
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var rows = Rows().Where(p => p.CustomerId == customerId);

        if (query.RequestId is { } filterRequestId)
        {
            rows = rows.Where(p => p.RequestId == filterRequestId);
        }

        var total = await rows.CountAsync(ct);
        var items = await rows
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PaymentDto>(items.Select(ToDto).ToList(), page, pageSize, total);
    }

    public async Task<PaymentDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var row = await Rows().FirstOrDefaultAsync(p => p.Id == id && p.CustomerId == customerId, ct)
            ?? throw new NotFoundException("Payment not found.");

        return ToDto(row);
    }

    public async Task<PaymentDto> CancelAsync(Guid id, CancellationToken ct = default)
    {
        var customerId = CustomerId;
        var payment = await FindAsync(id, customerId, ct);
        var before = Snapshot(payment);

        payment.Cancel();
        audit.Record("PaymentCancelled", nameof(Payment), payment.Id, before, Snapshot(payment));
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task<PaymentDto> SimulateAsync(
        Guid id, SimulatePaymentRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var customerId = CustomerId;
        var payment = await FindAsync(id, customerId, ct);
        var gateway = gateways.FirstOrDefault(g => g.Method == payment.PaymentMethod && g.IsAvailable)
            ?? throw new ServiceUnavailableException("This payment gateway is not available.");

        var targetStatus = request.Outcome switch
        {
            PaymentOutcome.Succeeded => PaymentStatus.Completed,
            PaymentOutcome.Failed => PaymentStatus.Failed,
            PaymentOutcome.Cancelled => PaymentStatus.Cancelled,
            _ => throw new ValidationException("Unknown payment outcome.")
        };

        if (payment.Status == targetStatus)
        {
            return await GetAsync(id, ct);
        }

        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            throw new ConflictException($"A payment that is {payment.Status} cannot be changed.");
        }

        var before = Snapshot(payment);

        switch (request.Outcome)
        {
            case PaymentOutcome.Succeeded:
                if (payment.Status == PaymentStatus.Pending)
                {
                    payment.Begin();
                }

                payment.Complete(clock.UtcNow);
                var title = await db.ServiceRequests.AsNoTracking()
                    .Where(r => r.Id == payment.ServiceRequestId)
                    .Select(r => r.Title)
                    .FirstAsync(ct);
                var amount = payment.Amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
                await notifier.ToCustomerAsync(
                    payment.CustomerId, NotificationType.PaymentCompleted, "Payment complete",
                    $"Your payment of {amount} {payment.Currency} for \"{title}\" is complete.",
                    nameof(Payment), payment.Id, ct);
                await notifier.ToProviderAsync(
                    payment.ProviderId, NotificationType.PaymentCompleted, "Payment received",
                    $"A payment of {amount} {payment.Currency} for \"{title}\" is complete.",
                    nameof(Payment), payment.Id, ct);
                break;
            case PaymentOutcome.Failed:
                payment.Fail(string.IsNullOrWhiteSpace(request.Reason)
                    ? "The payment was declined."
                    : request.Reason);
                break;
            case PaymentOutcome.Cancelled:
                payment.Cancel();
                break;
        }

        audit.Record("PaymentStatusChanged", nameof(Payment), payment.Id, before, Snapshot(payment));
        await db.SaveChangesAsync(ct);

        return await GetAsync(id, ct);
    }

    private async Task<(Guid PaymentId, bool Created)> StartCoreAsync(
        Guid customerId, Guid requestId, string idempotencyKey, IPaymentGateway gateway, CancellationToken ct)
    {
        var request = await db.ServiceRequests.AsNoTracking()
            .Where(r => r.Id == requestId && r.CustomerId == customerId)
            .Select(r => new { r.Id, r.Status })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Request not found.");

        var byKey = await db.Payments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, ct);

        if (byKey is not null)
        {
            if (byKey.ServiceRequestId != requestId)
            {
                throw new ConflictException("This Idempotency-Key was already used for a different request.");
            }

            return (byKey.Id, false);
        }

        if (request.Status != ServiceRequestStatus.Completed)
        {
            throw new ConflictException("Payment is available after the job is completed.");
        }

        var openPaymentId = await db.Payments.AsNoTracking()
            .Where(p => p.ServiceRequestId == requestId
                        && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing))
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (openPaymentId != Guid.Empty)
        {
            return (openPaymentId, false);
        }

        if (await db.Payments.AnyAsync(
                p => p.ServiceRequestId == requestId && p.Status == PaymentStatus.Completed, ct))
        {
            throw new ConflictException("This request has already been paid.");
        }

        var quote = await db.Quotes.AsNoTracking()
            .Where(q => q.ServiceRequestId == requestId && q.Status == QuoteStatus.Accepted)
            .Select(q => new { q.Id, q.ProviderId, q.Amount })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Accepted quote not found for this request.");

        var payment = new Payment(
            requestId, quote.Id, customerId, quote.ProviderId, quote.Amount,
            gateway.Method, PaymentRules.NewReference(clock.UtcNow), idempotencyKey);
        payment.Begin();

        db.Payments.Add(payment);
        audit.Record(
            "PaymentStarted", nameof(Payment), payment.Id, null,
            new { payment.QuoteId, payment.Amount, payment.PaymentMethod, payment.Status });
        await db.SaveChangesAsync(ct);

        return (payment.Id, true);
    }

    private async Task<Payment> FindAsync(Guid id, Guid customerId, CancellationToken ct) =>
        await db.Payments.FirstOrDefaultAsync(p => p.Id == id && p.CustomerId == customerId, ct)
        ?? throw new NotFoundException("Payment not found.");

    private IQueryable<PaymentRow> Rows() =>
        from payment in db.Payments.AsNoTracking()
        join request in db.ServiceRequests.AsNoTracking() on payment.ServiceRequestId equals request.Id
        join provider in db.ProviderProfiles.AsNoTracking() on payment.ProviderId equals provider.Id
        select new PaymentRow
        {
            Id = payment.Id,
            CustomerId = payment.CustomerId,
            RequestId = payment.ServiceRequestId,
            RequestTitle = request.Title,
            ProviderName = provider.BusinessName,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Method = payment.PaymentMethod,
            Status = payment.Status,
            Reference = payment.TransactionReference,
            FailureReason = payment.FailureReason,
            PaidAt = payment.PaidAt,
            CreatedAt = payment.CreatedAt
        };

    private PaymentDto ToDto(PaymentRow row)
    {
        var checkoutPath = row.Status is PaymentStatus.Pending or PaymentStatus.Processing
            ? gateways.FirstOrDefault(g => g.Method == row.Method && g.IsAvailable)?.CheckoutPath(row.Id)
            : null;

        return new PaymentDto(
            row.Id, row.RequestId, row.RequestTitle, row.ProviderName, row.Amount, row.Currency,
            row.Method, row.Status, row.Reference, row.FailureReason, row.PaidAt, row.CreatedAt, checkoutPath);
    }

    private static object Snapshot(Payment payment) =>
        new { payment.ServiceRequestId, payment.QuoteId, payment.Amount, payment.PaymentMethod, payment.Status };

    private sealed class PaymentRow
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid RequestId { get; set; }
        public string RequestTitle { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = Payment.DefaultCurrency;
        public PaymentMethod Method { get; set; }
        public PaymentStatus Status { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string? FailureReason { get; set; }
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
