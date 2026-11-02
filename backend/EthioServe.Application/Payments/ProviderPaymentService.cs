using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Payments;

public sealed class ProviderPaymentService(
    IApplicationDbContext db,
    ICurrentUser currentUser) : IProviderPaymentService
{
    private Guid ProviderId =>
        currentUser.ProviderId ?? throw new ForbiddenException("This account has no provider profile.");

    public async Task<PagedResult<ProviderPaymentDto>> ListAsync(
        ProviderPaymentQuery query, CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var rows = Rows().Where(p => p.ProviderId == providerId);
        var total = await rows.CountAsync(ct);
        var items = await rows
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ProviderPaymentDto>(
            items.Select(p => new ProviderPaymentDto(
                p.Id, p.RequestId, p.RequestTitle, p.CustomerName, p.Amount, p.Currency,
                p.Status, p.Reference, p.PaidAt, p.CreatedAt)).ToList(),
            page, pageSize, total);
    }

    public async Task<EarningsDto> EarningsAsync(CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var earnings = await db.Payments.AsNoTracking()
            .Where(p => p.ProviderId == providerId)
            .GroupBy(p => p.Currency)
            .Select(group => new
            {
                Currency = group.Key,
                TotalReceived = group.Where(p => p.Status == PaymentStatus.Completed)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                PaymentsReceived = group.Count(p => p.Status == PaymentStatus.Completed),
                AwaitingPaymentCount = group.Count(p =>
                    p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing),
                AwaitingPaymentAmount = group
                    .Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing)
                    .Sum(p => (decimal?)p.Amount) ?? 0m
            })
            .FirstOrDefaultAsync(ct);

        return earnings is null
            ? new EarningsDto(0m, 0, 0, 0m, Payment.DefaultCurrency)
            : new EarningsDto(
                earnings.TotalReceived, earnings.PaymentsReceived, earnings.AwaitingPaymentCount,
                earnings.AwaitingPaymentAmount, earnings.Currency);
    }

    private IQueryable<ProviderPaymentRow> Rows() =>
        from payment in db.Payments.AsNoTracking()
        join request in db.ServiceRequests.AsNoTracking() on payment.ServiceRequestId equals request.Id
        join customer in db.CustomerProfiles.AsNoTracking() on payment.CustomerId equals customer.Id
        select new ProviderPaymentRow
        {
            Id = payment.Id,
            ProviderId = payment.ProviderId,
            RequestId = payment.ServiceRequestId,
            RequestTitle = request.Title,
            CustomerName = customer.FirstName + " " + customer.LastName,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status,
            Reference = payment.TransactionReference,
            PaidAt = payment.PaidAt,
            CreatedAt = payment.CreatedAt
        };

    private sealed class ProviderPaymentRow
    {
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public Guid RequestId { get; set; }
        public string RequestTitle { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = Payment.DefaultCurrency;
        public PaymentStatus Status { get; set; }
        public string Reference { get; set; } = string.Empty;
        public DateTimeOffset? PaidAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
