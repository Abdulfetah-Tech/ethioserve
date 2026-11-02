using System.Net;
using System.Text.Json;
using EthioServe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Support;

public static class PaymentScenario
{
    public static async Task CompleteJobAsync(
        ApiFactory factory, HttpClient customer, HttpClient provider, Guid requestId)
    {
        var appointmentId = await AppointmentScenario.ScheduleAsync(
            provider, requestId, AppointmentScenario.InHours(26));
        var acceleratedStart = AppointmentScenario.InHours(1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE appointments SET start_at = {acceleratedStart}, end_at = {acceleratedStart.AddHours(2)} WHERE id = {appointmentId}");

        var (confirmed, _) = await AppointmentScenario.CustomerActionAsync(customer, appointmentId, "confirm");
        Assert.Equal(HttpStatusCode.OK, confirmed);

        var (started, _) = await AppointmentScenario.ProviderActionAsync(provider, appointmentId, "start");
        Assert.Equal(HttpStatusCode.OK, started);

        var (completed, _) = await AppointmentScenario.ProviderActionAsync(provider, appointmentId, "complete");
        Assert.Equal(HttpStatusCode.OK, completed);
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
