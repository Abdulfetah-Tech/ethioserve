using System.Net;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class PaymentConcurrencyTests(ApiFactory factory)
{
    [Fact]
    public async Task Different_idempotency_keys_share_one_open_payment_attempt()
    {
        var jobs = new JobBuilder(factory);
        using var world = await jobs.NewWorldAsync();
        var requestId = await jobs.AcceptedRequestAsync(world);
        await PaymentScenario.CompleteJobAsync(factory, world.Customer.Http, world.Provider.Http, requestId);

        var firstTask = PaymentScenario.StartAsync(world.Customer.Http, requestId, "parallel_1234");
        var secondTask = PaymentScenario.StartAsync(world.Customer.Http, requestId, "parallel_5678");
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.All(results, result => Assert.Contains(result.Status, new[] { HttpStatusCode.Created, HttpStatusCode.OK }));
        Assert.Equal(
            results[0].Body.GetProperty("id").GetGuid(),
            results[1].Body.GetProperty("id").GetGuid());
    }
}
