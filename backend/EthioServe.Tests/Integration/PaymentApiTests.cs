using System.Net;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class PaymentApiTests(ApiFactory factory)
{
    private readonly ApiSession _session = new(factory);
    private readonly JobBuilder _jobs = new(factory);

    [Fact]
    public async Task Customer_can_pay_a_completed_job_and_provider_sees_only_payment_summary()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        await PaymentScenario.CompleteRequestAsync(factory, requestId);

        var (created, pending) = await PaymentScenario.StartAsync(world.Customer.Http, requestId, "payment_1234");

        Assert.Equal(HttpStatusCode.Created, created);
        Assert.Equal(requestId, pending.GetProperty("requestId").GetGuid());
        Assert.Equal("Processing", pending.GetProperty("status").GetString());
        Assert.Equal("ETB", pending.GetProperty("currency").GetString());
        Assert.Equal(500m, pending.GetProperty("amount").GetDecimal());
        Assert.Equal($"/pay/{pending.GetProperty("id").GetGuid()}", pending.GetProperty("checkoutPath").GetString());
        Assert.Matches("^ES-[0-9]{8}-[A-HJ-NP-Z2-9]{12}$", pending.GetProperty("reference").GetString());

        var paymentId = pending.GetProperty("id").GetGuid();
        var (replayed, replayedPayment) = await PaymentScenario.StartAsync(
            world.Customer.Http, requestId, "payment_1234");
        var (sameOpenAttempt, sameAttempt) = await PaymentScenario.StartAsync(
            world.Customer.Http, requestId, "payment_5678");

        Assert.Equal(HttpStatusCode.OK, replayed);
        Assert.Equal(HttpStatusCode.OK, sameOpenAttempt);
        Assert.Equal(paymentId, replayedPayment.GetProperty("id").GetGuid());
        Assert.Equal(paymentId, sameAttempt.GetProperty("id").GetGuid());

        var (providerListStatus, providerList) = await ApiJson.SendAsync(
            world.Provider.Http, HttpMethod.Get, "/api/provider/payments");
        Assert.Equal(HttpStatusCode.OK, providerListStatus);
        var providerPayment = Assert.Single(providerList.GetProperty("items").EnumerateArray());
        Assert.Equal("Test User", providerPayment.GetProperty("customerName").GetString());
        Assert.False(providerPayment.TryGetProperty("customerPhone", out _));
        Assert.Equal("Processing", providerPayment.GetProperty("status").GetString());

        var (awaitingStatus, awaiting) = await ApiJson.SendAsync(
            world.Provider.Http, HttpMethod.Get, "/api/provider/payments/earnings");
        Assert.Equal(HttpStatusCode.OK, awaitingStatus);
        Assert.Equal(1, awaiting.GetProperty("awaitingPaymentCount").GetInt32());
        Assert.Equal(500m, awaiting.GetProperty("awaitingPaymentAmount").GetDecimal());
        Assert.Equal(0m, awaiting.GetProperty("totalReceived").GetDecimal());

        var (completeStatus, completed) = await ApiJson.SendAsync(
            world.Customer.Http,
            HttpMethod.Post,
            $"/api/customer/payments/{paymentId}/simulate",
            new { outcome = "Succeeded" });
        Assert.Equal(HttpStatusCode.OK, completeStatus);
        Assert.Equal("Completed", completed.GetProperty("status").GetString());
        Assert.True(completed.GetProperty("paidAt").ValueKind != System.Text.Json.JsonValueKind.Null);

        var (repeatedStatus, repeated) = await ApiJson.SendAsync(
            world.Customer.Http,
            HttpMethod.Post,
            $"/api/customer/payments/{paymentId}/simulate",
            new { outcome = "Succeeded" });
        Assert.Equal(HttpStatusCode.OK, repeatedStatus);
        Assert.Equal("Completed", repeated.GetProperty("status").GetString());

        var (paidStatus, paidEarnings) = await ApiJson.SendAsync(
            world.Provider.Http, HttpMethod.Get, "/api/provider/payments/earnings");
        Assert.Equal(HttpStatusCode.OK, paidStatus);
        Assert.Equal(1, paidEarnings.GetProperty("paymentsReceived").GetInt32());
        Assert.Equal(500m, paidEarnings.GetProperty("totalReceived").GetDecimal());

        var (duplicateStatus, _) = await PaymentScenario.StartAsync(
            world.Customer.Http, requestId, "payment_9999");
        Assert.Equal(HttpStatusCode.Conflict, duplicateStatus);

        using var anotherCustomer = await _session.CustomerAsync();
        var (privateStatus, _) = await ApiJson.SendAsync(
            anotherCustomer.Http, HttpMethod.Get, $"/api/customer/payments/{paymentId}");
        Assert.Equal(HttpStatusCode.NotFound, privateStatus);
    }

    [Fact]
    public async Task Payments_require_a_valid_idempotency_key_and_completed_job()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);

        var (tooEarly, _) = await PaymentScenario.StartAsync(world.Customer.Http, requestId, "payment_1234");
        Assert.Equal(HttpStatusCode.Conflict, tooEarly);

        await PaymentScenario.CompleteRequestAsync(factory, requestId);
        var (missingKey, problem) = await PaymentScenario.StartAsync(world.Customer.Http, requestId, null);

        Assert.Equal(HttpStatusCode.BadRequest, missingKey);
        Assert.Contains(
            problem.GetProperty("errors").EnumerateObject(),
            error => string.Equals(error.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Only_customers_can_use_customer_payment_routes()
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var provider = await _session.RegisteredProviderAsync();
        using var admin = await _session.AdminAsync();
        var path = "/api/customer/payments";

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { provider, admin })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, HttpMethod.Get, path);
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    [Fact]
    public async Task Only_providers_can_view_provider_payments()
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();
        using var admin = await _session.AdminAsync();
        const string path = "/api/provider/payments";

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, HttpMethod.Get, path);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { customer, admin })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, HttpMethod.Get, path);
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }
}
