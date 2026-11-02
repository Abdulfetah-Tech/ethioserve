using EthioServe.Domain.Enums;

namespace EthioServe.Application.Payments;

public interface IPaymentSettings
{
    /// <summary>The test gateway lets anyone "pay" without money, so it must never be on in production.</summary>
    bool AllowMock { get; }
}

public sealed class PaymentSettings(bool allowMock) : IPaymentSettings
{
    public bool AllowMock { get; } = allowMock;

    /// <summary>The mock gateway is never enabled in Production, even when explicitly configured.</summary>
    public static PaymentSettings Resolve(bool? configured, string environmentName)
    {
        var production = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
        var developmentOrTesting =
            string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

        return new(!production && (configured ?? developmentOrTesting));
    }
}

/// <summary>A way to take payment. Telebirr or Chapa will be more implementations of this.</summary>
public interface IPaymentGateway
{
    PaymentMethod Method { get; }

    bool IsAvailable { get; }

    /// <summary>The page in this app where the customer completes the payment.</summary>
    string CheckoutPath(Guid paymentId);
}

public sealed class MockPaymentGateway(IPaymentSettings settings) : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.Mock;

    public bool IsAvailable => settings.AllowMock;

    public string CheckoutPath(Guid paymentId) => $"/pay/{paymentId}";
}

public static class PaymentGateways
{
    public static IPaymentGateway? FirstAvailable(IEnumerable<IPaymentGateway> gateways) =>
        gateways.FirstOrDefault(g => g.IsAvailable);
}