using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EthioServe.Tests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class PasswordResetApiTests(ApiFactory factory)
{
    private const string Password = "Str0ngPassw0rd!";
    private const string NewPassword = "An0therStr0ngPass!";

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false
    });

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.local";

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register/customer", new
        {
            email,
            password = Password,
            firstName = "Abebe",
            lastName = "Kebede",
            phoneNumber = "0911223344"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Password_reset_is_reported_unavailable_and_rejected_without_email_delivery()
    {
        using var client = NewClient();
        var emails = factory.Services.GetRequiredService<TestEmailService>();
        emails.IsAvailable = false;

        try
        {
            using var capabilities = await client.GetAsync("/api/auth/capabilities");
            Assert.Equal(HttpStatusCode.OK, capabilities.StatusCode);
            Assert.False((await capabilities.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("passwordResetEnabled").GetBoolean());

            using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = NewEmail() });
            using var reset = await client.PostAsJsonAsync(
                "/api/auth/reset-password",
                new { token = "not-a-real-token", newPassword = NewPassword });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, forgot.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, reset.StatusCode);
        }
        finally
        {
            emails.IsAvailable = true;
        }
    }

    [Fact]
    public async Task Forgot_password_answers_identically_for_known_and_unknown_emails()
    {
        using var client = NewClient();
        var known = NewEmail();
        await RegisterAsync(client, known);

        using var knownResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = known });
        using var unknownResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = NewEmail() });

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);
        Assert.Equal(
            await knownResponse.Content.ReadAsStringAsync(),
            await unknownResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Full_reset_flow_works_over_http()
    {
        using var client = NewClient();
        var email = NewEmail();
        await RegisterAsync(client, email);

        using var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.Accepted, forgot.StatusCode);

        var emails = factory.Services.GetRequiredService<TestEmailService>();
        var token = TestEmailService.ExtractToken(emails.LastTo(email)!);

        using var reset = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        using var oldLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        using var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { email, password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task Reset_with_an_invalid_token_returns_400_with_a_token_error()
    {
        using var client = NewClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token = "not-a-real-token", newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Contains(errors.EnumerateObject(), p => string.Equals(p.Name, "Token", StringComparison.OrdinalIgnoreCase));
    }
}