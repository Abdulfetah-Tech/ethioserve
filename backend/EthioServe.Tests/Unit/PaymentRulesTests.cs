using EthioServe.Application.Payments;
using EthioServe.Domain.Enums;
using FluentValidation;

namespace EthioServe.Tests.Unit;

public sealed class PaymentRulesTests
{
    [Theory]
    [InlineData("abcdEFGH")]
    [InlineData("abcdefgh_1234")]
    [InlineData("ABCDEFGH-123456")]
    public void Idempotency_key_accepts_supported_characters(string key)
    {
        Assert.True(PaymentRules.IsValidKey(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("invalid key")]
    [InlineData("bad.key.123")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_extra")]
    public void Idempotency_key_rejects_missing_or_invalid_values(string? key)
    {
        Assert.False(PaymentRules.IsValidKey(key));
        Assert.Throws<ValidationException>(() => PaymentRules.NamespacedKey(Guid.NewGuid(), key));
    }

    [Fact]
    public void Namespaced_idempotency_key_is_customer_scoped()
    {
        var firstCustomer = Guid.NewGuid();
        var secondCustomer = Guid.NewGuid();

        Assert.NotEqual(
            PaymentRules.NamespacedKey(firstCustomer, "request_123"),
            PaymentRules.NamespacedKey(secondCustomer, "request_123"));
    }

    [Fact]
    public void Transaction_reference_is_readable_and_unique()
    {
        var now = DateTimeOffset.Parse("2026-11-02T10:00:00Z");
        var first = PaymentRules.NewReference(now);
        var second = PaymentRules.NewReference(now);

        Assert.Matches("^ES-20261102-[A-HJ-NP-Z2-9]{12}$", first);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Mock_gateway_is_disabled_in_production_even_if_explicitly_enabled()
    {
        Assert.False(PaymentSettings.Resolve(null, "Production").AllowMock);
        Assert.False(PaymentSettings.Resolve(true, "Production").AllowMock);
        Assert.True(PaymentSettings.Resolve(null, "Development").AllowMock);
        Assert.True(PaymentSettings.Resolve(null, "Testing").AllowMock);
    }

    [Fact]
    public void Simulation_requires_a_known_outcome()
    {
        var validator = new SimulatePaymentRequestValidator();

        Assert.False(validator.Validate(new SimulatePaymentRequest(null, null)).IsValid);
        Assert.False(validator.Validate(new SimulatePaymentRequest((PaymentOutcome)999, null)).IsValid);
        Assert.True(validator.Validate(new SimulatePaymentRequest(PaymentOutcome.Succeeded, null)).IsValid);
    }
}
