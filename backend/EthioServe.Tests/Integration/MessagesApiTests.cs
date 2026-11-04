using System.Net;
using System.Text.Json;
using EthioServe.Tests.Support;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class MessagesApiTests(ApiFactory factory)
{
    private readonly ApiSession _session = new(factory);
    private readonly ProviderScenario _providers = new(factory, new ApiSession(factory));

    private async Task<(SignedInClient Customer, SignedInClient First, SignedInClient Second, Guid RequestId)> SetupAsync()
    {
        using var admin = await _session.AdminAsync();
        var (_, serviceId) = await _providers.NewServiceAsync(admin.Http);
        var customer = await _session.CustomerAsync();
        var (regionId, cityId) = await ProviderScenario.LocationAsync(customer.Http, "Addis Ababa", "Addis Ababa");
        var requestId = await RequestScenario.CreateAsync(customer.Http, serviceId, regionId, cityId);
        var first = await _providers.VerifiedProviderAsync(admin.Http, ProviderScenario.Unique("First"), serviceId, 300m);
        var second = await _providers.VerifiedProviderAsync(admin.Http, ProviderScenario.Unique("Second"), serviceId, 400m);
        await QuoteScenario.SubmitAsync(first.Http, requestId);
        await QuoteScenario.SubmitAsync(second.Http, requestId);

        return (customer, first, second, requestId);
    }

    [Fact]
    public async Task Customer_and_each_quoting_provider_only_see_their_private_thread()
    {
        var (customer, first, second, requestId) = await SetupAsync();
        using (customer)
        using (first)
        using (second)
        {
            var firstProviderId = first.ProfileId!.Value;
            var secondProviderId = second.ProfileId!.Value;

            var (firstStatus, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Post,
                $"/api/requests/{requestId}/messages", new { providerId = firstProviderId, body = "First provider only" });
            var (secondStatus, _) = await ApiJson.SendAsync(customer.Http, HttpMethod.Post,
                $"/api/requests/{requestId}/messages", new { providerId = secondProviderId, body = "Second provider only" });
            Assert.Equal(HttpStatusCode.OK, firstStatus);
            Assert.Equal(HttpStatusCode.OK, secondStatus);

            var firstProviderReply = await ApiJson.SendAsync(first.Http, HttpMethod.Post,
                $"/api/requests/{requestId}/messages", new { providerId = firstProviderId, body = "First reply" });
            Assert.Equal(HttpStatusCode.OK, firstProviderReply.Status);

            var firstThread = await ApiJson.GetAsync(customer.Http,
                $"/api/requests/{requestId}/messages?providerId={firstProviderId}");
            var secondThread = await ApiJson.GetAsync(customer.Http,
                $"/api/requests/{requestId}/messages?providerId={secondProviderId}");
            var providerThread = await ApiJson.GetAsync(first.Http,
                $"/api/requests/{requestId}/messages?providerId={firstProviderId}");

            Assert.Equal(HttpStatusCode.OK, firstThread.Status);
            Assert.Equal(HttpStatusCode.OK, secondThread.Status);
            Assert.Equal(HttpStatusCode.OK, providerThread.Status);
            Assert.Equal(new[] { "First provider only", "First reply" },
                MessageBodies(firstThread.Body).Order().ToArray());
            Assert.Equal(new[] { "Second provider only" }, MessageBodies(secondThread.Body));
            Assert.Equal(new[] { "First provider only", "First reply" },
                MessageBodies(providerThread.Body).Order().ToArray());
        }
    }

    [Fact]
    public async Task Provider_cannot_access_another_provider_thread_and_nonparticipants_cannot_read()
    {
        var (customer, first, second, requestId) = await SetupAsync();
        using (customer)
        using (first)
        using (second)
        using (var outsider = await _session.CustomerAsync())
        {
            var forgedSend = await ApiJson.SendAsync(first.Http, HttpMethod.Post,
                $"/api/requests/{requestId}/messages", new
                {
                    providerId = second.ProfileId,
                    body = "Attempted cross-provider message"
                });
            var forgedRead = await ApiJson.GetAsync(first.Http,
                $"/api/requests/{requestId}/messages?providerId={second.ProfileId}");
            var outsiderRead = await ApiJson.GetAsync(outsider.Http,
                $"/api/requests/{requestId}/messages?providerId={first.ProfileId}");

            Assert.Equal(HttpStatusCode.NotFound, forgedSend.Status);
            Assert.Equal(HttpStatusCode.NotFound, forgedRead.Status);
            Assert.Equal(HttpStatusCode.NotFound, outsiderRead.Status);
        }
    }

    private static string[] MessageBodies(JsonElement result) =>
        result.GetProperty("items").EnumerateArray()
            .Select(message => message.GetProperty("body").GetString()!)
            .ToArray();
}
