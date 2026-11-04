using System.Net;
using System.Text.Json;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ProviderSelfApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);

    public static IEnumerable<object[]> ProviderRoutes()
    {
        yield return new object[] { "GET", "/api/providers/me" };
        yield return new object[] { "PUT", "/api/providers/me" };
        yield return new object[] { "PUT", "/api/providers/me/availability" };
        yield return new object[] { "GET", "/api/providers/me/availability-calendar" };
        yield return new object[] { "PUT", "/api/providers/me/availability-calendar" };
        yield return new object[] { "DELETE", "/api/providers/me/availability-calendar" };
        yield return new object[] { "POST", "/api/providers/me/verification-request" };
        yield return new object[] { "GET", "/api/providers/me/services" };
        yield return new object[] { "POST", "/api/providers/me/services" };
        yield return new object[] { "PUT", $"/api/providers/me/services/{UnknownId}" };
        yield return new object[] { "DELETE", $"/api/providers/me/services/{UnknownId}" };
    }

    private static object? BodyFor(string method) => method is "POST" or "PUT" ? new { } : null;

    private static bool HasField(JsonElement problem, string field) =>
        problem.GetProperty("errors").EnumerateObject().Any(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

    private static object UpdateBody(
        int regionId, int cityId,
        string businessName = "Abebe Plumbing",
        string description = "Experienced plumber serving Addis Ababa for ten years.",
        string phone = "0911556677",
        int years = 10,
        string address = "Bole, near Edna Mall",
        decimal? latitude = 9.0192m,
        decimal? longitude = 38.7525m) =>
        new
        {
            businessName,
            description,
            phoneNumber = phone,
            yearsOfExperience = years,
            regionId,
            cityId,
            subCityId = (int?)null,
            address,
            latitude,
            longitude
        };

    private static async Task<JsonElement> GetProfileAsync(HttpClient client)
    {
        var (status, body) = await ApiJson.SendAsync(client, HttpMethod.Get, "/api/providers/me");
        Assert.Equal(HttpStatusCode.OK, status);

        return body;
    }

    private static async Task<(int RegionId, int CityId)> LocationOfAsync(HttpClient client)
    {
        var profile = await GetProfileAsync(client);

        return (profile.GetProperty("regionId").GetInt32(), profile.GetProperty("cityId").GetInt32());
    }

    private async Task<Guid> ServiceIdAsync(string name)
    {
        using var anonymous = _session.CreateAnonymousClient();
        var (_, body, _) = await ApiJson.GetAsync(anonymous, $"/api/services?search={Uri.EscapeDataString(name)}");

        return body.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<Guid> AddOfferAsync(HttpClient client, Guid serviceId, decimal price = 300m)
    {
        var (status, body) = await ApiJson.SendAsync(
            client, HttpMethod.Post, "/api/providers/me/services", new { serviceId, price, description = "Offer" });

        Assert.Equal(HttpStatusCode.Created, status);

        return body.GetProperty("id").GetGuid();
    }

    // ------------------------------------------------------------- access control

    [Theory]
    [MemberData(nameof(ProviderRoutes))]
    public async Task Only_providers_can_use_provider_routes(string method, string url)
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();
        using var admin = await _session.AdminAsync();

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, new HttpMethod(method), url, BodyFor(method));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { customer, admin })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, new HttpMethod(method), url, BodyFor(method));
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    [Fact]
    public async Task A_provider_role_without_a_profile_is_forbidden_not_a_server_error()
    {
        using var roleOnly = await _session.ProviderAsync();

        var (status, _) = await ApiJson.SendAsync(roleOnly.Http, HttpMethod.Get, "/api/providers/me");

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    // ------------------------------------------------------------- profile

    [Fact]
    public async Task A_new_provider_sees_an_unverified_available_profile_in_addis_ababa()
    {
        using var provider = await _session.RegisteredProviderAsync("Kebede Plumbing");

        var profile = await GetProfileAsync(provider.Http);

        Assert.Equal(provider.ProfileId, profile.GetProperty("id").GetGuid());
        Assert.Equal("Kebede Plumbing", profile.GetProperty("businessName").GetString());
        Assert.Equal("Unverified", profile.GetProperty("verificationStatus").GetString());
        Assert.True(profile.GetProperty("isAvailable").GetBoolean());
        Assert.Equal(0m, profile.GetProperty("averageRating").GetDecimal());
        Assert.Equal(0, profile.GetProperty("activeServiceCount").GetInt32());
        Assert.Equal("Addis Ababa", profile.GetProperty("regionName").GetString());
        Assert.Equal("Addis Ababa", profile.GetProperty("cityName").GetString());
    }

    [Fact]
    public async Task A_provider_can_update_their_profile()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var (regionId, cityId) = await LocationOfAsync(provider.Http);

        var (status, body) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me",
            UpdateBody(regionId, cityId, businessName: "Renamed Plumbing", years: 15, address: "Piassa"));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Renamed Plumbing", body.GetProperty("businessName").GetString());
        Assert.Equal(15, body.GetProperty("yearsOfExperience").GetInt32());
        Assert.Equal("Piassa", body.GetProperty("address").GetString());
        Assert.Equal(9.0192m, body.GetProperty("latitude").GetDecimal());

        var reloaded = await GetProfileAsync(provider.Http);
        Assert.Equal("Renamed Plumbing", reloaded.GetProperty("businessName").GetString());
    }

    [Fact]
    public async Task Invalid_profile_input_returns_400_with_field_errors()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var (regionId, cityId) = await LocationOfAsync(provider.Http);

        var (badPhone, phoneBody) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me",
            UpdateBody(regionId, cityId, phone: "not-a-phone"));
        var (badYears, yearsBody) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me",
            UpdateBody(regionId, cityId, years: 61));
        var (badCoordinates, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me",
            UpdateBody(regionId, cityId, latitude: 9.0m, longitude: null));

        Assert.Equal(HttpStatusCode.BadRequest, badPhone);
        Assert.True(HasField(phoneBody, "phoneNumber"));
        Assert.Equal(HttpStatusCode.BadRequest, badYears);
        Assert.True(HasField(yearsBody, "yearsOfExperience"));
        Assert.Equal(HttpStatusCode.BadRequest, badCoordinates);
    }

    [Fact]
    public async Task A_city_from_another_region_is_rejected()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var (regionId, _) = await LocationOfAsync(provider.Http);

        using var anonymous = _session.CreateAnonymousClient();
        var (_, regions, _) = await ApiJson.GetAsync(anonymous, "/api/locations/regions");
        var oromiaId = regions.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Oromia").GetProperty("id").GetInt32();
        var (_, cities, _) = await ApiJson.GetAsync(anonymous, $"/api/locations/regions/{oromiaId}/cities");
        var adamaId = cities.EnumerateArray().First().GetProperty("id").GetInt32();

        var (status, body) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me",
            UpdateBody(regionId, adamaId));

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(HasField(body, "cityId"));
    }

    [Fact]
    public async Task Providers_cannot_set_their_own_status_rating_or_owner()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var (regionId, cityId) = await LocationOfAsync(provider.Http);

        var (status, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me", new
        {
            businessName = "Sneaky Plumbing",
            description = "Experienced plumber serving Addis Ababa for ten years.",
            phoneNumber = "0911556677",
            yearsOfExperience = 10,
            regionId,
            cityId,
            address = "Bole",
            verificationStatus = "Verified",
            averageRating = 5.0,
            totalReviews = 999,
            userId = Guid.NewGuid(),
            id = Guid.NewGuid()
        });

        var profile = await GetProfileAsync(provider.Http);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Sneaky Plumbing", profile.GetProperty("businessName").GetString());
        Assert.Equal("Unverified", profile.GetProperty("verificationStatus").GetString());
        Assert.Equal(0m, profile.GetProperty("averageRating").GetDecimal());
        Assert.Equal(0, profile.GetProperty("totalReviews").GetInt32());
        Assert.Equal(provider.ProfileId, profile.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Availability_can_be_switched_off_and_on()
    {
        using var provider = await _session.RegisteredProviderAsync();

        var (_, off) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me/availability", new { isAvailable = false });
        var (_, on) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me/availability", new { isAvailable = true });

        Assert.False(off.GetProperty("isAvailable").GetBoolean());
        Assert.True(on.GetProperty("isAvailable").GetBoolean());
    }

    [Fact]
    public async Task A_provider_can_manage_their_weekly_hours_and_date_exceptions()
    {
        using var provider = await _session.RegisteredProviderAsync();
        const string url = "/api/providers/me/availability-calendar";

        var (initialStatus, initial) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, url);
        Assert.Equal(HttpStatusCode.OK, initialStatus);
        Assert.False(initial.GetProperty("isConfigured").GetBoolean());

        var request = new
        {
            weeklyHours = new[]
            {
                new
                {
                    dayOfWeek = 1,
                    windows = new[]
                    {
                        new { startMinute = 540, endMinute = 720 },
                        new { startMinute = 780, endMinute = 1020 }
                    }
                }
            },
            dateExceptions = new object[]
            {
                new { date = new DateOnly(2027, 1, 1), isClosed = true, windows = Array.Empty<object>() },
                new
                {
                    date = new DateOnly(2027, 1, 2),
                    isClosed = false,
                    windows = new[] { new { startMinute = 600, endMinute = 840 } }
                }
            }
        };
        var (saveStatus, saved) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, url, request);

        Assert.Equal(HttpStatusCode.OK, saveStatus);
        Assert.Equal(2, saved.GetProperty("weeklyHours")[0].GetProperty("windows").GetArrayLength());
        Assert.Equal(2, saved.GetProperty("dateExceptions").GetArrayLength());

        var (_, reloaded) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, url);
        Assert.Equal(780, reloaded.GetProperty("weeklyHours")[0].GetProperty("windows")[1]
            .GetProperty("startMinute").GetInt32());
        Assert.True(reloaded.GetProperty("dateExceptions")[0].GetProperty("isClosed").GetBoolean());

        var (clearStatus, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Delete, url);
        var (_, cleared) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, url);

        Assert.Equal(HttpStatusCode.NoContent, clearStatus);
        Assert.False(cleared.GetProperty("isConfigured").GetBoolean());
    }

    [Fact]
    public async Task Invalid_overlapping_hours_and_closed_date_windows_are_rejected()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var (status, body) = await ApiJson.SendAsync(
            provider.Http, HttpMethod.Put, "/api/providers/me/availability-calendar", new
            {
                weeklyHours = new[]
                {
                    new
                    {
                        dayOfWeek = 1,
                        windows = new[]
                        {
                            new { startMinute = 540, endMinute = 720 },
                            new { startMinute = 700, endMinute = 800 }
                        }
                    }
                },
                dateExceptions = new[]
                {
                    new
                    {
                        date = new DateOnly(2027, 1, 1),
                        isClosed = true,
                        windows = new[] { new { startMinute = 540, endMinute = 600 } }
                    }
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Contains(
            body.GetProperty("errors").EnumerateObject(),
            error => error.Name.StartsWith("weeklyHours", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            body.GetProperty("errors").EnumerateObject(),
            error => error.Name.StartsWith("dateExceptions", StringComparison.OrdinalIgnoreCase));
    }

    // ------------------------------------------------------------- offers

    [Fact]
    public async Task A_provider_can_add_list_update_and_remove_service_offers()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var serviceId = await ServiceIdAsync("Pipe repair");

        var offerId = await AddOfferAsync(provider.Http, serviceId, 300m);

        var (_, list) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me/services");
        var offer = list.EnumerateArray().Single();
        Assert.Equal(offerId, offer.GetProperty("id").GetGuid());
        Assert.Equal("Pipe repair", offer.GetProperty("serviceName").GetString());
        Assert.Equal("Plumbing", offer.GetProperty("categoryName").GetString());
        Assert.Equal(300m, offer.GetProperty("price").GetDecimal());

        var (updated, body) = await ApiJson.SendAsync(provider.Http, HttpMethod.Put, $"/api/providers/me/services/{offerId}",
            new { price = 450m, description = "Now with parts included", isActive = false });
        Assert.Equal(HttpStatusCode.OK, updated);
        Assert.Equal(450m, body.GetProperty("price").GetDecimal());
        Assert.False(body.GetProperty("isActive").GetBoolean());

        var profile = await GetProfileAsync(provider.Http);
        Assert.Equal(0, profile.GetProperty("activeServiceCount").GetInt32()); // the only offer is switched off

        var (removed, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Delete, $"/api/providers/me/services/{offerId}");
        var (_, afterDelete) = await ApiJson.SendAsync(provider.Http, HttpMethod.Get, "/api/providers/me/services");
        Assert.Equal(HttpStatusCode.NoContent, removed);
        Assert.Equal(0, afterDelete.GetArrayLength());
    }

    [Fact]
    public async Task Offering_the_same_service_twice_is_a_conflict()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var serviceId = await ServiceIdAsync("Pipe repair");
        await AddOfferAsync(provider.Http, serviceId);

        var (status, _) = await ApiJson.SendAsync(
            provider.Http, HttpMethod.Post, "/api/providers/me/services", new { serviceId, price = 100m });

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task Unknown_and_negative_inputs_are_rejected_for_offers()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var serviceId = await ServiceIdAsync("Pipe repair");

        var (unknown, unknownBody) = await ApiJson.SendAsync(
            provider.Http, HttpMethod.Post, "/api/providers/me/services", new { serviceId = Guid.NewGuid(), price = 100m });
        var (negative, negativeBody) = await ApiJson.SendAsync(
            provider.Http, HttpMethod.Post, "/api/providers/me/services", new { serviceId, price = -5m });

        Assert.Equal(HttpStatusCode.BadRequest, unknown);
        Assert.True(HasField(unknownBody, "serviceId"));
        Assert.Equal(HttpStatusCode.BadRequest, negative);
        Assert.True(HasField(negativeBody, "price"));
    }

    [Fact]
    public async Task Deactivated_catalog_services_cannot_be_offered()
    {
        using var admin = await _session.AdminAsync();
        using var provider = await _session.RegisteredProviderAsync();

        var (_, category) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/categories",
            new { name = $"Cat-{Guid.NewGuid():N}"[..20] });
        var categoryId = category.GetProperty("id").GetGuid();
        var (_, service) = await ApiJson.SendAsync(admin.Http, HttpMethod.Post, "/api/admin/services",
            new { categoryId, name = "Short-lived", basePrice = 10m, pricingType = "Fixed" });
        var serviceId = service.GetProperty("id").GetGuid();
        await ApiJson.SendAsync(admin.Http, HttpMethod.Post, $"/api/admin/services/{serviceId}/deactivate");

        var (status, body) = await ApiJson.SendAsync(
            provider.Http, HttpMethod.Post, "/api/providers/me/services", new { serviceId, price = 100m });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.True(HasField(body, "serviceId"));
    }

    [Fact]
    public async Task One_provider_cannot_see_or_change_another_providers_offers()
    {
        using var owner = await _session.RegisteredProviderAsync();
        using var intruder = await _session.RegisteredProviderAsync();
        var serviceId = await ServiceIdAsync("Pipe repair");
        var offerId = await AddOfferAsync(owner.Http, serviceId, 300m);

        var (update, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Put, $"/api/providers/me/services/{offerId}",
            new { price = 1m, isActive = false });
        var (delete, _) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Delete, $"/api/providers/me/services/{offerId}");
        var (_, intruderList) = await ApiJson.SendAsync(intruder.Http, HttpMethod.Get, "/api/providers/me/services");
        var (_, ownerList) = await ApiJson.SendAsync(owner.Http, HttpMethod.Get, "/api/providers/me/services");

        Assert.Equal(HttpStatusCode.NotFound, update);
        Assert.Equal(HttpStatusCode.NotFound, delete);
        Assert.Equal(0, intruderList.GetArrayLength());
        Assert.Equal(300m, ownerList.EnumerateArray().Single().GetProperty("price").GetDecimal());
    }

    // ------------------------------------------------------------- verification request

    [Fact]
    public async Task An_incomplete_profile_cannot_request_verification()
    {
        using var provider = await _session.RegisteredProviderAsync();

        using var response = await provider.Http.PostAsync("/api/providers/me/verification-request", null);
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("description", text);
        Assert.Contains("address", text);
        Assert.Contains("service", text);
    }

    [Fact]
    public async Task A_complete_profile_can_request_verification_once_and_it_is_audited()
    {
        using var provider = await _session.RegisteredProviderAsync();
        var (regionId, cityId) = await LocationOfAsync(provider.Http);
        await ApiJson.SendAsync(provider.Http, HttpMethod.Put, "/api/providers/me", UpdateBody(regionId, cityId));
        await AddOfferAsync(provider.Http, await ServiceIdAsync("Pipe repair"));

        var (first, firstBody) = await ApiJson.SendAsync(provider.Http, HttpMethod.Post, "/api/providers/me/verification-request");
        var (second, _) = await ApiJson.SendAsync(provider.Http, HttpMethod.Post, "/api/providers/me/verification-request");

        Assert.Equal(HttpStatusCode.OK, first);
        Assert.Equal("Pending", firstBody.GetProperty("verificationStatus").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityId == provider.ProfileId.ToString() && a.EntityName == nameof(ProviderProfile))
            .ToListAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("ProviderVerificationRequested", entry.Action);
        Assert.Equal(provider.UserId, entry.UserId);
        Assert.Contains("Unverified", entry.OldValues);
        Assert.Contains("Pending", entry.NewValues);
    }
}