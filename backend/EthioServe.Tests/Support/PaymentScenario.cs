using System.Net;
using System.Text.Json;
using EthioServe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Support;

public static class PaymentScenario
{
    public static async Task CompleteRequestAsync(ApiFactory factory, Guid requestId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var completedAt = DateTimeOffset.UtcNow;

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE service_requests SET status = 'Completed', completed_at = {completedAt} WHERE id = {requestId}");
    }

    public static async Task<(HttpStatusCode Status, JsonElement Body)> StartAsync(
        HttpClient customer, Guid requestId, string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/customer/payments/requests/{requestId}");

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        using var response = await customer.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);

        return (response.StatusCode, document.RootElement.Clone());
    }
}
