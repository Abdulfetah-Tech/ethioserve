using EthioServe.Application.Common;
using EthioServe.Domain.Enums;
using FluentValidation;

namespace EthioServe.Application.Payments;

/// <summary>What the (test) gateway reports back. A real gateway's webhook will end up in the same place.</summary>
public enum PaymentOutcome
{
    Succeeded,
    Failed,
    Cancelled
}

// The outcome is optional in the type on purpose: a missing value must be refused, never read as "Succeeded".
public sealed record SimulatePaymentRequest(PaymentOutcome? Outcome, string? Reason);

public sealed class SimulatePaymentRequestValidator : AbstractValidator<SimulatePaymentRequest>
{
    public SimulatePaymentRequestValidator()
    {
        RuleFor(x => x.Outcome).NotNull();
        RuleFor(x => x.Outcome)
            .Must(outcome => outcome is null || Enum.IsDefined(outcome.Value))
            .WithMessage("Unknown outcome.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

/// <summary>A payment as its customer sees it. The test gateway has no card or phone details, so there are none here.</summary>
public sealed record PaymentDto(
    Guid Id,
    Guid RequestId,
    string RequestTitle,
    string ProviderName,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    PaymentStatus Status,
    string Reference,
    string? FailureReason,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    string? CheckoutPath);

public sealed record StartPaymentResult(PaymentDto Payment, bool Created);

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class PaymentQuery
{
    public Guid? RequestId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface ICustomerPaymentService
{
    /// <summary>
    /// Starts paying for a completed job, or returns the attempt that already exists for this key or is still open.
    /// The amount, provider and customer all come from the server, never from the request.
    /// </summary>
    Task<StartPaymentResult> StartAsync(Guid requestId, string? idempotencyKey, CancellationToken ct = default);

    Task<PagedResult<PaymentDto>> ListAsync(PaymentQuery query, CancellationToken ct = default);

    Task<PaymentDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>The customer walks away from an open attempt.</summary>
    Task<PaymentDto> CancelAsync(Guid id, CancellationToken ct = default);

    /// <summary>The test gateway's answer. Repeating the same answer is safe, as real gateways repeat theirs.</summary>
    Task<PaymentDto> SimulateAsync(Guid id, SimulatePaymentRequest request, CancellationToken ct = default);
}

/// <summary>A payment as the provider sees it. The customer's name, and nothing else about them.</summary>
public sealed record ProviderPaymentDto(
    Guid Id,
    Guid RequestId,
    string RequestTitle,
    string CustomerName,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string Reference,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt);

public sealed record EarningsDto(
    decimal TotalReceived,
    int PaymentsReceived,
    int AwaitingPaymentCount,
    decimal AwaitingPaymentAmount,
    string Currency);

public sealed class ProviderPaymentQuery
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

public interface IProviderPaymentService
{
    /// <summary>Money received (and refunded), newest first.</summary>
    Task<PagedResult<ProviderPaymentDto>> ListAsync(ProviderPaymentQuery query, CancellationToken ct = default);

    Task<EarningsDto> EarningsAsync(CancellationToken ct = default);
}