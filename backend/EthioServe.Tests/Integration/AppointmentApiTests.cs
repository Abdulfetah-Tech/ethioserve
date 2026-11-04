using System.Net;
using System.Text.Json;
using EthioServe.Application.Appointments;
using EthioServe.Domain.Entities;
using EthioServe.Infrastructure.Persistence;
using EthioServe.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EthioServe.Tests.Integration;

[Collection(ApiCollection.Name)]
public sealed class AppointmentApiTests(ApiFactory factory)
{
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    private readonly ApiSession _session = new(factory);
    private readonly JobBuilder _jobs = new(factory);

    public static IEnumerable<object[]> ProviderRoutes()
    {
        yield return new object[] { "POST", $"/api/providers/me/requests/{UnknownId}/appointment" };
        yield return new object[] { "GET", "/api/providers/me/appointments" };
        yield return new object[] { "GET", $"/api/providers/me/appointments/{UnknownId}" };
        yield return new object[] { "POST", $"/api/providers/me/appointments/{UnknownId}/reschedule" };
        yield return new object[] { "POST", $"/api/providers/me/appointments/{UnknownId}/cancel" };
        yield return new object[] { "POST", $"/api/providers/me/appointments/{UnknownId}/start" };
        yield return new object[] { "POST", $"/api/providers/me/appointments/{UnknownId}/complete" };
    }

    public static IEnumerable<object[]> CustomerRoutes()
    {
        yield return new object[] { "GET", "/api/appointments" };
        yield return new object[] { "GET", $"/api/appointments/{UnknownId}" };
        yield return new object[] { "POST", $"/api/appointments/{UnknownId}/confirm" };
        yield return new object[] { "POST", $"/api/appointments/{UnknownId}/cancel" };
    }

    private static object? BodyFor(string method) => method == "POST" ? new { } : null;

    private static bool HasField(JsonElement problem, string field) =>
        problem.GetProperty("errors").EnumerateObject().Any(p => string.Equals(p.Name, field, StringComparison.OrdinalIgnoreCase));

    private static string Status(JsonElement appointment) => appointment.GetProperty("status").GetString()!;

    // ------------------------------------------------------------- access control

    [Theory]
    [MemberData(nameof(ProviderRoutes))]
    public async Task Only_providers_can_use_the_provider_appointment_routes(string method, string url)
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

    [Theory]
    [MemberData(nameof(CustomerRoutes))]
    public async Task Only_customers_can_use_the_customer_appointment_routes(string method, string url)
    {
        using var anonymous = _session.CreateAnonymousClient();
        using var provider = await _session.RegisteredProviderAsync();
        using var admin = await _session.AdminAsync();

        var (anonymousStatus, _) = await ApiJson.SendAsync(anonymous, new HttpMethod(method), url, BodyFor(method));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousStatus);

        foreach (var user in new[] { provider, admin })
        {
            var (status, _) = await ApiJson.SendAsync(user.Http, new HttpMethod(method), url, BodyFor(method));
            Assert.Equal(HttpStatusCode.Forbidden, status);
        }
    }

    // ------------------------------------------------------------- scheduling

    [Fact]
    public async Task The_provider_schedules_the_accepted_job_and_both_sides_see_the_right_details()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world, minutes: 90);
        var start = AppointmentScenario.InHours(26);

        var (status, body) = await AppointmentScenario.ScheduleRawAsync(
            world.Provider.Http, requestId, AppointmentScenario.Body(start, "  Bring spare pipes  "));

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.Equal("Scheduled", Status(body));
        Assert.Equal(start, body.GetProperty("startAt").GetDateTimeOffset());
        Assert.Equal(start.AddMinutes(90), body.GetProperty("endAt").GetDateTimeOffset());
        Assert.Equal("Bole, near the main road", body.GetProperty("address").GetString());
        Assert.Equal("Bring spare pipes", body.GetProperty("notes").GetString());
        Assert.Equal("Test User", body.GetProperty("customerName").GetString());
        Assert.Equal("0911223344", body.GetProperty("customerPhone").GetString());
        Assert.Equal(500m, body.GetProperty("amount").GetDecimal());
        Assert.Equal("Scheduled", await QuoteScenario.RequestStatusAsync(world.Customer.Http, requestId));

        var forCustomer = Assert.Single(await AppointmentScenario.CustomerListAsync(world.Customer.Http));
        Assert.Equal(requestId, forCustomer.GetProperty("requestId").GetGuid());
        Assert.Equal("0911556677", forCustomer.GetProperty("providerPhone").GetString());
        Assert.False(string.IsNullOrEmpty(forCustomer.GetProperty("providerName").GetString()));
        Assert.Equal(start, forCustomer.GetProperty("startAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task The_provider_calendar_blocks_closed_dates_and_allows_configured_exception_hours()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var nextDayAtTen = DateTime.SpecifyKind(
            DateTime.UtcNow.AddHours(3).Date.AddDays(1).AddHours(10), DateTimeKind.Unspecified);
        var start = new DateTimeOffset(nextDayAtTen, TimeSpan.FromHours(3));
        var localDate = DateOnly.FromDateTime(nextDayAtTen);
        const string calendarUrl = "/api/providers/me/availability-calendar";

        var (_, closed) = await ApiJson.SendAsync(world.Provider.Http, HttpMethod.Put, calendarUrl, new
        {
            weeklyHours = Array.Empty<object>(),
            dateExceptions = new[]
            {
                new { date = localDate, isClosed = true, windows = Array.Empty<object>() }
            }
        });
        Assert.Equal(0, closed.GetProperty("weeklyHours").GetArrayLength());

        var (closedStatus, closedBody) = await AppointmentScenario.ScheduleRawAsync(
            world.Provider.Http, requestId, AppointmentScenario.Body(start));
        Assert.Equal(HttpStatusCode.BadRequest, closedStatus);
        Assert.True(HasField(closedBody, "startAt"));

        var (openedStatus, _) = await ApiJson.SendAsync(world.Provider.Http, HttpMethod.Put, calendarUrl, new
        {
            weeklyHours = Array.Empty<object>(),
            dateExceptions = new[]
            {
                new
                {
                    date = localDate,
                    isClosed = false,
                    windows = new[] { new { startMinute = 9 * 60, endMinute = 13 * 60 } }
                }
            }
        });
        Assert.Equal(HttpStatusCode.OK, openedStatus);
        Assert.Equal(HttpStatusCode.Created,
            (await AppointmentScenario.ScheduleRawAsync(
                world.Provider.Http, requestId, AppointmentScenario.Body(start))).Status);

        var outsideRequestId = await _jobs.AcceptedRequestAsync(world);
        var outsideHours = new DateTimeOffset(
            DateTime.SpecifyKind(nextDayAtTen.Date.AddHours(14), DateTimeKind.Unspecified),
            TimeSpan.FromHours(3));
        var (outsideStatus, _) = await AppointmentScenario.ScheduleRawAsync(
            world.Provider.Http, outsideRequestId, AppointmentScenario.Body(outsideHours));

        Assert.Equal(HttpStatusCode.BadRequest, outsideStatus);
    }

    [Fact]
    public async Task A_time_sent_with_a_local_offset_is_stored_in_utc_to_the_minute()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var raw = new DateTimeOffset(DateTime.UtcNow.AddHours(30), TimeSpan.Zero).ToOffset(TimeSpan.FromHours(3));

        var (status, body) = await AppointmentScenario.ScheduleRawAsync(world.Provider.Http, requestId, AppointmentScenario.Body(raw));

        Assert.Equal(HttpStatusCode.Created, status);
        var stored = body.GetProperty("startAt").GetDateTimeOffset();
        Assert.Equal(AppointmentRules.Normalize(raw), stored);
        Assert.Equal(0, stored.UtcDateTime.Second);
    }

    [Fact]
    public async Task Times_that_are_too_soon_in_the_past_or_too_far_away_are_refused_and_change_nothing()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var http = world.Provider.Http;

        var (soon, soonBody) = await AppointmentScenario.ScheduleRawAsync(http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(0.5)));
        var (past, _) = await AppointmentScenario.ScheduleRawAsync(http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(-24)));
        var (far, farBody) = await AppointmentScenario.ScheduleRawAsync(http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(24 * 91)));
        var (missing, _) = await AppointmentScenario.ScheduleRawAsync(http, requestId, new { notes = "no time" });
        var (longNote, longNoteBody) = await AppointmentScenario.ScheduleRawAsync(
            http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(30), new string('x', 1001)));

        Assert.Equal(HttpStatusCode.BadRequest, soon);
        Assert.True(HasField(soonBody, "startAt"));
        Assert.Equal(HttpStatusCode.BadRequest, past);
        Assert.Equal(HttpStatusCode.BadRequest, far);
        Assert.True(HasField(farBody, "startAt"));
        Assert.Equal(HttpStatusCode.BadRequest, missing);
        Assert.Equal(HttpStatusCode.BadRequest, longNote);
        Assert.True(HasField(longNoteBody, "notes"));
        Assert.Equal("Accepted", await QuoteScenario.RequestStatusAsync(world.Customer.Http, requestId));
        Assert.Empty(await AppointmentScenario.ProviderListAsync(http));
    }

    [Fact]
    public async Task Only_the_provider_whose_quote_was_accepted_can_schedule()
    {
        using var world = await _jobs.NewWorldAsync();
        using var other = await _jobs.ExtraProviderAsync(world);

        var requestId = await RequestScenario.CreateAsync(
            world.Customer.Http, world.ServiceId, world.RegionId, world.CityId, ProviderScenario.Unique("Two quotes"));
        var chosen = await QuoteScenario.SubmitAsync(world.Provider.Http, requestId, 300m);
        await QuoteScenario.SubmitAsync(other.Http, requestId, 280m);

        var start = AppointmentScenario.InHours(30);
        var (beforeAcceptance, _) = await AppointmentScenario.ScheduleRawAsync(world.Provider.Http, requestId, AppointmentScenario.Body(start));

        await ApiJson.SendAsync(world.Customer.Http, HttpMethod.Post, $"/api/requests/{requestId}/quotes/{chosen}/accept");

        var (loser, _) = await AppointmentScenario.ScheduleRawAsync(other.Http, requestId, AppointmentScenario.Body(start));
        var (winner, _) = await AppointmentScenario.ScheduleRawAsync(world.Provider.Http, requestId, AppointmentScenario.Body(start));

        Assert.Equal(HttpStatusCode.NotFound, beforeAcceptance);
        Assert.Equal(HttpStatusCode.NotFound, loser);
        Assert.Equal(HttpStatusCode.Created, winner);
    }

    [Fact]
    public async Task A_job_can_only_have_one_appointment_at_a_time()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(30));

        var (again, _) = await AppointmentScenario.ScheduleRawAsync(
            world.Provider.Http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(60)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, again);
    }

    // ------------------------------------------------------------- no double booking

    [Fact]
    public async Task Overlapping_times_are_refused_touching_times_are_allowed_and_cancelling_frees_the_time()
    {
        using var world = await _jobs.NewWorldAsync();
        var job1 = await _jobs.AcceptedRequestAsync(world);
        var job2 = await _jobs.AcceptedRequestAsync(world);
        var job3 = await _jobs.AcceptedRequestAsync(world);
        var http = world.Provider.Http;
        var t = AppointmentScenario.InHours(48);

        var first = await AppointmentScenario.ScheduleAsync(http, job1, t); // t .. t+2h

        var (partial, partialBody) = await AppointmentScenario.ScheduleRawAsync(http, job2, AppointmentScenario.Body(t.AddHours(1)));
        var (sameStart, _) = await AppointmentScenario.ScheduleRawAsync(http, job2, AppointmentScenario.Body(t));
        var (touching, _) = await AppointmentScenario.ScheduleRawAsync(http, job2, AppointmentScenario.Body(t.AddHours(2)));
        var (overlapsFirstFromBefore, _) = await AppointmentScenario.ScheduleRawAsync(http, job3, AppointmentScenario.Body(t.AddHours(-1)));

        await AppointmentScenario.ProviderActionAsync(http, first, "cancel");
        var (afterCancel, _) = await AppointmentScenario.ScheduleRawAsync(http, job3, AppointmentScenario.Body(t.AddHours(-1)));

        Assert.Equal(HttpStatusCode.Conflict, partial);
        Assert.Contains("already have an appointment", partialBody.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.Conflict, sameStart);
        Assert.Equal(HttpStatusCode.Created, touching);
        Assert.Equal(HttpStatusCode.Conflict, overlapsFirstFromBefore);
        Assert.Equal(HttpStatusCode.Created, afterCancel);
    }

    [Fact]
    public async Task Different_providers_can_work_at_the_same_time()
    {
        using var world = await _jobs.NewWorldAsync();
        using var other = await _jobs.ExtraProviderAsync(world);
        var job1 = await _jobs.AcceptedRequestAsync(world);
        var job2 = await _jobs.AcceptedRequestAsync(world, provider: other);
        var t = AppointmentScenario.InHours(48);

        var (first, _) = await AppointmentScenario.ScheduleRawAsync(world.Provider.Http, job1, AppointmentScenario.Body(t));
        var (second, _) = await AppointmentScenario.ScheduleRawAsync(other.Http, job2, AppointmentScenario.Body(t));

        Assert.Equal(HttpStatusCode.Created, first);
        Assert.Equal(HttpStatusCode.Created, second);
    }

    // ------------------------------------------------------------- confirming, rescheduling, cancelling

    [Fact]
    public async Task The_customer_confirms_once_and_the_request_stays_scheduled()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var id = await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(30));

        var (confirmed, body) = await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "confirm");
        var (again, _) = await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "confirm");
        var (_, providerView) = await AppointmentScenario.ProviderGetAsync(world.Provider.Http, id);

        Assert.Equal(HttpStatusCode.OK, confirmed);
        Assert.Equal("Confirmed", Status(body));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again);
        Assert.Equal("Confirmed", Status(providerView));
        Assert.Equal("Scheduled", await QuoteScenario.RequestStatusAsync(world.Customer.Http, requestId));
    }

    [Fact]
    public async Task Rescheduling_asks_the_customer_to_confirm_again_and_cannot_clash_with_other_jobs()
    {
        using var world = await _jobs.NewWorldAsync();
        var job1 = await _jobs.AcceptedRequestAsync(world);
        var job2 = await _jobs.AcceptedRequestAsync(world);
        var http = world.Provider.Http;
        var t = AppointmentScenario.InHours(48);

        var first = await AppointmentScenario.ScheduleAsync(http, job1, t, "Original note");
        await AppointmentScenario.ScheduleAsync(http, job2, t.AddHours(5));
        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, first, "confirm");

        var newTime = t.AddHours(10);
        var (moved, movedBody) = await AppointmentScenario.ProviderActionAsync(
            http, first, "reschedule", AppointmentScenario.Body(newTime, "New note"));
        var (clash, _) = await AppointmentScenario.ProviderActionAsync(
            http, first, "reschedule", AppointmentScenario.Body(t.AddHours(5).AddMinutes(30)));
        var (past, _) = await AppointmentScenario.ProviderActionAsync(
            http, first, "reschedule", AppointmentScenario.Body(AppointmentScenario.InHours(0.2)));
        var (_, afterClash) = await AppointmentScenario.ProviderGetAsync(http, first);

        Assert.Equal(HttpStatusCode.OK, moved);
        Assert.Equal("Scheduled", Status(movedBody));
        Assert.Equal(newTime, movedBody.GetProperty("startAt").GetDateTimeOffset());
        Assert.Equal(newTime.AddMinutes(120), movedBody.GetProperty("endAt").GetDateTimeOffset());
        Assert.Equal("New note", movedBody.GetProperty("notes").GetString());
        Assert.Equal(HttpStatusCode.Conflict, clash);
        Assert.Equal(HttpStatusCode.BadRequest, past);
        Assert.Equal(newTime, afterClash.GetProperty("startAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("provider")]
    public async Task Cancelling_returns_the_job_to_accepted_and_lets_the_provider_book_a_new_time(string who)
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var id = await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(30));

        var (status, body) = who == "customer"
            ? await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "cancel")
            : await AppointmentScenario.ProviderActionAsync(world.Provider.Http, id, "cancel");
        var (again, _) = await AppointmentScenario.ProviderActionAsync(world.Provider.Http, id, "cancel");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("Cancelled", Status(body));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again);
        Assert.Equal("Accepted", await QuoteScenario.RequestStatusAsync(world.Customer.Http, requestId));

        var rebooked = await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(60));
        var history = await AppointmentScenario.ProviderListAsync(world.Provider.Http, "?scope=History");
        var upcoming = await AppointmentScenario.ProviderListAsync(world.Provider.Http);

        Assert.NotEqual(id, rebooked);
        Assert.Equal(id, Assert.Single(history).GetProperty("id").GetGuid());
        Assert.Equal(rebooked, Assert.Single(upcoming).GetProperty("id").GetGuid());
    }

    // ------------------------------------------------------------- doing the job

    [Fact]
    public async Task The_job_runs_from_scheduled_to_completed_and_the_request_follows()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var http = world.Provider.Http;
        var id = await AppointmentScenario.ScheduleAsync(http, requestId, AppointmentScenario.InHours(1.5)); // within the 2 hour window

        var (startUnconfirmed, _) = await AppointmentScenario.ProviderActionAsync(http, id, "start");
        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "confirm");
        var (completeEarly, _) = await AppointmentScenario.ProviderActionAsync(http, id, "complete");

        var (started, startedBody) = await AppointmentScenario.ProviderActionAsync(http, id, "start");
        var requestWhileRunning = await QuoteScenario.RequestStatusAsync(world.Customer.Http, requestId);
        var (customerCancel, _) = await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "cancel");
        var (providerCancel, _) = await AppointmentScenario.ProviderActionAsync(http, id, "cancel");
        var (reschedule, _) = await AppointmentScenario.ProviderActionAsync(http, id, "reschedule", AppointmentScenario.Body(AppointmentScenario.InHours(30)));

        var (completed, completedBody) = await AppointmentScenario.ProviderActionAsync(http, id, "complete");
        var requestAtEnd = await QuoteScenario.RequestStatusAsync(world.Customer.Http, requestId);
        var (scheduleAgain, _) = await AppointmentScenario.ScheduleRawAsync(http, requestId, AppointmentScenario.Body(AppointmentScenario.InHours(30)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, startUnconfirmed);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, completeEarly);
        Assert.Equal(HttpStatusCode.OK, started);
        Assert.Equal("InProgress", Status(startedBody));
        Assert.Equal("InProgress", requestWhileRunning);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, customerCancel);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, providerCancel);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reschedule);
        Assert.Equal(HttpStatusCode.OK, completed);
        Assert.Equal("Completed", Status(completedBody));
        Assert.Equal("Completed", requestAtEnd);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, scheduleAgain);

        Assert.Empty(await AppointmentScenario.ProviderListAsync(http));
        Assert.Single(await AppointmentScenario.ProviderListAsync(http, "?scope=History"));
        Assert.Single(await AppointmentScenario.CustomerListAsync(world.Customer.Http, "?scope=History"));
    }

    [Fact]
    public async Task Work_cannot_start_more_than_two_hours_before_the_agreed_time()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var id = await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(72));
        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "confirm");

        var (status, body) = await AppointmentScenario.ProviderActionAsync(world.Provider.Http, id, "start");
        var (_, after) = await AppointmentScenario.ProviderGetAsync(world.Provider.Http, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.Contains("2 hours", body.GetProperty("detail").GetString());
        Assert.Equal("Confirmed", Status(after));
        Assert.Equal("Scheduled", await QuoteScenario.RequestStatusAsync(world.Customer.Http, requestId));
    }

    // ------------------------------------------------------------- listing

    [Fact]
    public async Task Upcoming_appointments_are_listed_soonest_first_and_can_be_filtered_by_request()
    {
        using var world = await _jobs.NewWorldAsync();
        var jobA = await _jobs.AcceptedRequestAsync(world);
        var jobB = await _jobs.AcceptedRequestAsync(world);
        var jobC = await _jobs.AcceptedRequestAsync(world);
        var http = world.Provider.Http;

        await AppointmentScenario.ScheduleAsync(http, jobA, AppointmentScenario.InHours(72));
        await AppointmentScenario.ScheduleAsync(http, jobB, AppointmentScenario.InHours(48));
        await AppointmentScenario.ScheduleAsync(http, jobC, AppointmentScenario.InHours(96));

        var all = await AppointmentScenario.ProviderListAsync(http);
        var onlyA = await AppointmentScenario.ProviderListAsync(http, $"?requestId={jobA}");
        var forCustomer = await AppointmentScenario.CustomerListAsync(world.Customer.Http);
        var (_, paged) = await ApiJson.SendAsync(http, HttpMethod.Get, "/api/providers/me/appointments?pageSize=2&page=2");
        var (badScope, _) = await ApiJson.SendAsync(http, HttpMethod.Get, "/api/providers/me/appointments?scope=banana");

        Assert.Equal(new[] { jobB, jobA, jobC }, all.Select(a => a.GetProperty("requestId").GetGuid()).ToArray());
        Assert.Equal(jobA, Assert.Single(onlyA).GetProperty("requestId").GetGuid());
        Assert.Equal(new[] { jobB, jobA, jobC }, forCustomer.Select(a => a.GetProperty("requestId").GetGuid()).ToArray());
        Assert.Equal(3, paged.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, paged.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, badScope);
    }

    // ------------------------------------------------------------- ownership

    [Fact]
    public async Task Other_customers_and_other_providers_cannot_see_or_change_an_appointment()
    {
        using var world = await _jobs.NewWorldAsync();
        using var otherProvider = await _jobs.ExtraProviderAsync(world);
        using var otherCustomer = await _session.CustomerAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var id = await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(30));

        var (customerGet, _) = await AppointmentScenario.CustomerGetAsync(otherCustomer.Http, id);
        var (customerConfirm, _) = await AppointmentScenario.CustomerActionAsync(otherCustomer.Http, id, "confirm");
        var (customerCancel, _) = await AppointmentScenario.CustomerActionAsync(otherCustomer.Http, id, "cancel");

        var (providerGet, _) = await AppointmentScenario.ProviderGetAsync(otherProvider.Http, id);
        var (providerReschedule, _) = await AppointmentScenario.ProviderActionAsync(
            otherProvider.Http, id, "reschedule", AppointmentScenario.Body(AppointmentScenario.InHours(50)));
        var (providerCancel, _) = await AppointmentScenario.ProviderActionAsync(otherProvider.Http, id, "cancel");
        var (providerStart, _) = await AppointmentScenario.ProviderActionAsync(otherProvider.Http, id, "start");
        var (providerComplete, _) = await AppointmentScenario.ProviderActionAsync(otherProvider.Http, id, "complete");

        Assert.All(
            new[] { customerGet, customerConfirm, customerCancel, providerGet, providerReschedule, providerCancel, providerStart, providerComplete },
            status => Assert.Equal(HttpStatusCode.NotFound, status));
        Assert.Empty(await AppointmentScenario.CustomerListAsync(otherCustomer.Http));
        Assert.Empty(await AppointmentScenario.ProviderListAsync(otherProvider.Http));

        var (_, untouched) = await AppointmentScenario.ProviderGetAsync(world.Provider.Http, id);
        Assert.Equal("Scheduled", Status(untouched));
    }

    // ------------------------------------------------------------- audit

    [Fact]
    public async Task Every_step_is_audited_by_the_person_who_took_it_without_any_contact_details()
    {
        using var world = await _jobs.NewWorldAsync();
        var requestId = await _jobs.AcceptedRequestAsync(world);
        var id = await AppointmentScenario.ScheduleAsync(world.Provider.Http, requestId, AppointmentScenario.InHours(30));
        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "confirm");
        await AppointmentScenario.ProviderActionAsync(world.Provider.Http, id, "reschedule", AppointmentScenario.Body(AppointmentScenario.InHours(40)));
        await AppointmentScenario.CustomerActionAsync(world.Customer.Http, id, "cancel");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityName == nameof(Appointment) && a.EntityId == id.ToString())
            .OrderBy(a => a.Id)
            .ToListAsync();

        Assert.Equal(
            new[] { "AppointmentScheduled", "AppointmentConfirmed", "AppointmentRescheduled", "AppointmentCancelled" },
            entries.Select(e => e.Action).ToArray());
        Assert.Equal(
            new[] { world.Provider.UserId, world.Customer.UserId, world.Provider.UserId, world.Customer.UserId },
            entries.Select(e => e.UserId!.Value).ToArray());
        Assert.Contains("Scheduled", entries[1].OldValues);
        Assert.Contains("Confirmed", entries[1].NewValues);

        Assert.All(entries, e =>
        {
            var text = (e.OldValues ?? string.Empty) + (e.NewValues ?? string.Empty);

            Assert.DoesNotContain("0911", text);
            Assert.DoesNotContain("Bole", text);
        });
    }
}