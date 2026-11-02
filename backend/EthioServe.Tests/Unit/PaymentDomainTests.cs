using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;

namespace EthioServe.Tests.Unit;

public sealed class PaymentDomainTests
{
    [Fact]
    public void Payment_moves_from_pending_through_processing_to_completed()
    {
        var payment = CreatePayment();
        var paidAt = DateTimeOffset.UtcNow;

        Assert.Equal(PaymentStatus.Pending, payment.Status);
        payment.Begin();
        Assert.Equal(PaymentStatus.Processing, payment.Status);

        payment.Complete(paidAt);

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal(paidAt, payment.PaidAt);
    }

    [Fact]
    public void Payment_can_fail_or_cancel_while_open_but_not_after_completion()
    {
        var failed = CreatePayment();
        failed.Fail("Declined");
        Assert.Equal(PaymentStatus.Failed, failed.Status);
        Assert.Equal("Declined", failed.FailureReason);

        var cancelled = CreatePayment();
        cancelled.Begin();
        cancelled.Cancel();
        Assert.Equal(PaymentStatus.Cancelled, cancelled.Status);

        Assert.Throws<DomainException>(() => failed.Cancel());
        Assert.Throws<DomainException>(() => cancelled.Fail("Declined"));
    }

    [Fact]
    public void Completed_payment_can_be_refunded_only_once()
    {
        var payment = CreatePayment();
        payment.Begin();
        payment.Complete(DateTimeOffset.UtcNow);

        payment.Refund();

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Throws<DomainException>(payment.Refund);
    }

    [Fact]
    public void Failure_reason_is_required_and_bounded()
    {
        var payment = CreatePayment();

        Assert.Throws<DomainException>(() => payment.Fail(" "));
        Assert.Throws<DomainException>(() => payment.Fail(new string('x', Payment.MaxFailureReasonLength + 1)));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void Payment_cannot_complete_without_starting()
    {
        var payment = CreatePayment();

        Assert.Throws<DomainException>(() => payment.Complete(DateTimeOffset.UtcNow));
    }

    private static Payment CreatePayment() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 500m, PaymentMethod.Mock, "ES-REF-001");
}
