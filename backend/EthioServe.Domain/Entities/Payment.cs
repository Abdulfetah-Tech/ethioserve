using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class Payment : BaseEntity
{
    public const string DefaultCurrency = "ETB";

    public const int MaxFailureReasonLength = 300;

    private Payment() { }

    public Payment(
        Guid serviceRequestId, Guid quoteId, Guid customerId, Guid providerId, decimal amount,
        PaymentMethod paymentMethod, string transactionReference, string? idempotencyKey = null)
    {
        ServiceRequestId = serviceRequestId;
        QuoteId = quoteId;
        CustomerId = customerId;
        ProviderId = providerId;
        Amount = Guard.Positive(amount, nameof(amount));
        Currency = DefaultCurrency;
        PaymentMethod = paymentMethod;
        TransactionReference = Guard.NotBlank(transactionReference, nameof(transactionReference));
        IdempotencyKey = idempotencyKey;
        Status = PaymentStatus.Pending;
    }

    public Guid ServiceRequestId { get; private set; }

    public Guid QuoteId { get; private set; }

    public Guid CustomerId { get; private set; }

    public Guid ProviderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = DefaultCurrency;

    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Changed only through the methods below.</summary>
    public PaymentStatus Status { get; private set; }

    public string TransactionReference { get; private set; } = string.Empty;

    public string? IdempotencyKey { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>PostgreSQL xmin: optimistic concurrency token.</summary>
    public uint Version { get; private set; }

    /// <summary>The customer reached the gateway.</summary>
    public void Begin()
    {
        Require(PaymentStatus.Pending, "started");
        Status = PaymentStatus.Processing;
    }

    public void Complete(DateTimeOffset now)
    {
        Require(PaymentStatus.Processing, "completed");

        Status = PaymentStatus.Completed;
        PaidAt = now;
        FailureReason = null;
    }

    public void Fail(string reason)
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            throw new DomainException($"A payment that is {Status} cannot be marked as failed.");
        }

        var text = Guard.NotBlank(reason, nameof(reason));

        if (text.Length > MaxFailureReasonLength)
        {
            throw new DomainException($"The reason cannot be longer than {MaxFailureReasonLength} characters.");
        }

        FailureReason = text;
        Status = PaymentStatus.Failed;
    }

    public void Cancel()
    {
        if (Status is not (PaymentStatus.Pending or PaymentStatus.Processing))
        {
            throw new DomainException($"A payment that is {Status} cannot be cancelled.");
        }

        Status = PaymentStatus.Cancelled;
    }

    public void Refund()
    {
        Require(PaymentStatus.Completed, "refunded");
        Status = PaymentStatus.Refunded;
    }

    private void Require(PaymentStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException($"A payment that is {Status} cannot be {action}.");
        }
    }
}