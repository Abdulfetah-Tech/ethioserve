using System.Net;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class ReviewApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly JobBuilder _jobs = new(factory);

    [Fact]
    public async Task Only_admins_can_list_or_moderate_reviews()
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var customer = await _session.CustomerAsync();
        using var provider = await _session.RegisteredProviderAsync();

        foreach (var (method, url) in new[]
                 {
                     (HttpMethod.Get, "/api/admin/reviews"),
                     (HttpMethod.Post, $"/api/admin/reviews/{UnknownId}/hide"),
                     (HttpMethod.Post, $"/api/admin/reviews/{UnknownId}/restore")
                 })
        {
            var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, method, url);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

            var (customerStatus, _) = await ApiJson.SendAsync(customer.Http, method, url);
            Assert.Equal(HttpStatusCode.Forbidden, customerStatus);

            var (providerStatus, _) = await ApiJson.SendAsync(provider.Http, method, url);
            Assert.Equal(HttpStatusCode.Forbidden, providerStatus);
        }
    }

    [Fact]
    public async Task Admins_can_hide_and_restore_reviews_and_provider_ratings_follow_visibility()
    {
        using var world = await _jobs.NewWorldAsync();
        using var admin = await _session.AdminAsync();
        var requestId = await CompleteJobAsync(world);
        var (_, created) = await ApiJson.SendAsync(
            world.Customer.Http,
            HttpMethod.Post,
            $"/api/requests/{requestId}/reviews",
            new { rating = 5, comment = "A private audit-safe comment." });
        var reviewId = created.GetProperty("id").GetGuid();

        var (visibleStatus, visibleQueue) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Get, "/api/admin/reviews?isVisible=true");
        var visibleReview = visibleQueue.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("id").GetGuid() == reviewId);
        Assert.Equal(HttpStatusCode.OK, visibleStatus);
        Assert.Equal("A private audit-safe comment.", visibleReview.GetProperty("comment").GetString());
        Assert.False(string.IsNullOrWhiteSpace(visibleReview.GetProperty("providerBusinessName").GetString()));
        Assert.True(visibleReview.GetProperty("isVisible").GetBoolean());

        var (hideStatus, hidden) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, $"/api/admin/reviews/{reviewId}/hide");
        Assert.Equal(HttpStatusCode.OK, hideStatus);
        Assert.False(hidden.GetProperty("isVisible").GetBoolean());

        using var anonymous = _session.CreateAnonymousClient();
        var (_, publicAfterHide) = await ApiJson.SendAsync(
            anonymous, HttpMethod.Get, $"/api/providers/{world.Provider.ProfileId}/reviews");
        Assert.Equal(0, publicAfterHide.GetProperty("totalCount").GetInt32());

        var (_, hiddenQueue) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Get, "/api/admin/reviews?isVisible=false");
        Assert.Contains(
            hiddenQueue.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == reviewId);

        var (repeatHideStatus, _) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, $"/api/admin/reviews/{reviewId}/hide");
        Assert.Equal(HttpStatusCode.OK, repeatHideStatus);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var providerProfile = await db.ProviderProfiles.SingleAsync(p => p.Id == world.Provider.ProfileId);
            var hideAudit = await db.AuditLogs.SingleAsync(a =>
                a.EntityName == nameof(Review) && a.EntityId == reviewId.ToString() && a.Action == "ReviewHidden");
            Assert.Equal(0m, providerProfile.AverageRating);
            Assert.Equal(0, providerProfile.TotalReviews);
            Assert.Equal("ReviewHidden", hideAudit.Action);
            Assert.Equal(admin.UserId, hideAudit.UserId);
            Assert.DoesNotContain("private audit-safe comment", hideAudit.NewValues);
        }

        var (restoreStatus, restored) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, $"/api/admin/reviews/{reviewId}/restore");
        Assert.Equal(HttpStatusCode.OK, restoreStatus);
        Assert.True(restored.GetProperty("isVisible").GetBoolean());

        var (_, publicAfterRestore) = await ApiJson.SendAsync(
            anonymous, HttpMethod.Get, $"/api/providers/{world.Provider.ProfileId}/reviews");
        Assert.Equal(1, publicAfterRestore.GetProperty("totalCount").GetInt32());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restoredProvider = await verifyDb.ProviderProfiles.SingleAsync(p => p.Id == world.Provider.ProfileId);
        var decisions = await verifyDb.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Review) && a.EntityId == reviewId.ToString())
            .OrderBy(a => a.Id)
            .Select(a => a.Action)
            .ToArrayAsync();
        Assert.Equal(5m, restoredProvider.AverageRating);
        Assert.Equal(1, restoredProvider.TotalReviews);
        Assert.Equal(new[] { "ReviewCreated", "ReviewHidden", "ReviewRestored" }, decisions);
    }

    [Fact]
    public async Task A_customer_can_review_a_completed_job_and_the_public_provider_rating_is_updated()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await CompleteJobAsync(world);

        var (created, review) = await ApiJson.SendAsync(
            world.Customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/reviews",
            new { rating = 5, comment = "  Arrived on time and fixed the problem.  " });

        using var anonymous = _session.CreateAnonymousClient();
        var (listed, page) = await ApiJson.SendAsync(
            anonymous, HttpMethod.Get, $"/api/providers/{world.Provider.ProfileId}/reviews");

        Assert.Equal(HttpStatusCode.Created, created);
        Assert.Equal(5, review.GetProperty("rating").GetInt32());
        Assert.Equal("Arrived on time and fixed the problem.", review.GetProperty("comment").GetString());
        Assert.Equal(HttpStatusCode.OK, listed);
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(5, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("rating").GetInt32());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provider = await db.ProviderProfiles.SingleAsync(p => p.Id == world.Provider.ProfileId);
        var audit = await db.AuditLogs.SingleAsync(a => a.EntityName == nameof(Review));
        var notification = await db.Notifications.SingleAsync(n => n.Type == NotificationType.ReviewReceived);

        Assert.Equal(5m, provider.AverageRating);
        Assert.Equal(1, provider.TotalReviews);
        Assert.Equal("ReviewCreated", audit.Action);
        Assert.DoesNotContain("Arrived on time", audit.NewValues);
        Assert.Equal(world.Provider.UserId, notification.UserId);
    }

    [Fact]
    public async Task A_review_is_allowed_once_and_only_after_the_customer_completed_the_job()
    {
        using var world = await _jobs.NewWorldAsync();
        var notDone = await _jobs.AcceptedRequestAsync(world);

        var (tooEarly, _) = await ApiJson.SendAsync(
            world.Customer.Http, HttpMethod.Post, $"/api/requests/{notDone}/reviews", new { rating = 4 });

        var completed = await CompleteJobAsync(world);
        var (first, _) = await ApiJson.SendAsync(
            world.Customer.Http, HttpMethod.Post, $"/api/requests/{completed}/reviews", new { rating = 4 });
        var (duplicate, _) = await ApiJson.SendAsync(
            world.Customer.Http, HttpMethod.Post, $"/api/requests/{completed}/reviews", new { rating = 3 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooEarly);
        Assert.Equal(HttpStatusCode.Created, first);
        Assert.Equal(HttpStatusCode.Conflict, duplicate);
    }

    [Fact]
    public async Task Reviews_enforce_customer_ownership_and_validate_the_body()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await CompleteJobAsync(world);
        using var otherCustomer = await _session.CustomerAsync();
        using var provider = await _session.RegisteredProviderAsync();
        using var admin = await _session.AdminAsync();
        using var anonymous = _session.CreateAnonymousClient();

        var (anonymousStatus, _) = await ApiJson.SendAsync(
            anonymous, HttpMethod.Post, $"/api/requests/{requestId}/reviews", new { rating = 5 });
        var (providerStatus, _) = await ApiJson.SendAsync(
            provider.Http, HttpMethod.Post, $"/api/requests/{requestId}/reviews", new { rating = 5 });
        var (adminStatus, _) = await ApiJson.SendAsync(
            admin.Http, HttpMethod.Post, $"/api/requests/{requestId}/reviews", new { rating = 5 });
        var (otherCustomerStatus, _) = await ApiJson.SendAsync(
            otherCustomer.Http, HttpMethod.Post, $"/api/requests/{requestId}/reviews", new { rating = 5 });
        var (invalid, invalidBody) = await ApiJson.SendAsync(
            world.Customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/reviews",
            new { rating = 6, comment = new string('x', Review.MaxCommentLength + 1) });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);
        Assert.Equal(HttpStatusCode.Forbidden, providerStatus);
        Assert.Equal(HttpStatusCode.Forbidden, adminStatus);
        Assert.Equal(HttpStatusCode.NotFound, otherCustomerStatus);
        Assert.Equal(HttpStatusCode.BadRequest, invalid);
        Assert.True(invalidBody.GetProperty("errors").TryGetProperty("Rating", out _));
        Assert.True(invalidBody.GetProperty("errors").TryGetProperty("Comment", out _));
    }

    private async Task<Guid> CompleteJobAsync(JobWorld world)
    {
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var appointmentId = await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(1.5));
        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, appointmentId, "confirm");
        await AppointmentScenario.ProviderActionAsync(world.Provider.Http, appointmentId, "start");
        var (completed, _) = await AppointmentScenario.ProviderActionAsync(world.Provider.Http, appointmentId, "complete");
        Assert.Equal(HttpStatusCode.OK, completed);

        return requestId;
    }
}
