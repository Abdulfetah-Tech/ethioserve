using EthioServe.Application.Appointments;
using EthioServe.Domain.Entities;

namespace EthioServe.Tests.Unit;

public sealed class AppointmentRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_start_needs_at_least_one_hour_of_notice()
    {
        Assert.NotNull(AppointmentRules.StartProblem(Now.AddMinutes(59), Now));
        Assert.NotNull(AppointmentRules.StartProblem(Now.AddHours(-3), Now));
        Assert.Null(AppointmentRules.StartProblem(Now.AddHours(1), Now));
        Assert.Null(AppointmentRules.StartProblem(Now.AddHours(30), Now));
    }

    [Fact]
    public void A_start_cannot_be_more_than_ninety_days_away()
    {
        Assert.Null(AppointmentRules.StartProblem(Now.AddDays(90), Now));
        Assert.Contains("90 days", AppointmentRules.StartProblem(Now.AddDays(90).AddSeconds(1), Now));
    }

    [Fact]
    public void The_end_is_the_start_plus_the_estimated_duration()
    {
        Assert.Equal(Now.AddMinutes(135), AppointmentRules.EndFor(Now, 135));
    }

    [Fact]
    public void Work_can_start_two_hours_before_the_agreed_time_but_not_earlier()
    {
        Assert.True(AppointmentRules.CanStartWork(Now.AddHours(2), Now));
        Assert.True(AppointmentRules.CanStartWork(Now.AddMinutes(30), Now));
        Assert.True(AppointmentRules.CanStartWork(Now.AddHours(-1), Now));
        Assert.False(AppointmentRules.CanStartWork(Now.AddHours(2).AddSeconds(1), Now));
        Assert.False(AppointmentRules.CanStartWork(Now.AddDays(3), Now));
    }

    [Fact]
    public void Periods_overlap_unless_they_only_touch()
    {
        var a1 = Now;
        var a2 = Now.AddHours(2);

        Assert.True(AppointmentRules.Overlaps(a1, a2, a1.AddHours(1), a2.AddHours(1)));
        Assert.True(AppointmentRules.Overlaps(a1, a2, a1.AddMinutes(30), a2.AddMinutes(-30)));
        Assert.True(AppointmentRules.Overlaps(a1, a2, a1, a2));
        Assert.True(AppointmentRules.Overlaps(a1.AddHours(1), a2.AddHours(1), a1, a2));
        Assert.False(AppointmentRules.Overlaps(a1, a2, a2, a2.AddHours(2)));
        Assert.False(AppointmentRules.Overlaps(a2, a2.AddHours(2), a1, a2));
        Assert.False(AppointmentRules.Overlaps(a1, a2, a2.AddHours(1), a2.AddHours(3)));
    }

    [Fact]
    public void Times_are_converted_to_utc_and_cut_to_the_minute()
    {
        var local = new DateTimeOffset(2026, 12, 1, 14, 30, 45, 500, TimeSpan.FromHours(3));

        var normalized = AppointmentRules.Normalize(local);

        Assert.Equal(TimeSpan.Zero, normalized.Offset);
        Assert.Equal(new DateTimeOffset(2026, 12, 1, 11, 30, 0, TimeSpan.Zero), normalized);
    }

    [Fact]
    public void A_time_that_is_already_normal_stays_the_same()
    {
        Assert.Equal(Now, AppointmentRules.Normalize(Now));
    }

    [Fact]
    public void Messages_show_the_time_in_addis_ababa()
    {
        Assert.Equal("1 Dec 2026, 14:00", AppointmentRules.FormatLocal(new DateTimeOffset(2026, 12, 1, 11, 0, 0, TimeSpan.Zero)));
        Assert.Equal("2 Dec 2026, 00:30", AppointmentRules.FormatLocal(new DateTimeOffset(2026, 12, 1, 21, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void A_weekly_window_must_contain_the_entire_appointment_in_addis_time()
    {
        var schedule = new ProviderAvailabilitySchedule(
            [new WeeklyAvailabilityDay(2, [new AvailabilityWindow(9 * 60, 17 * 60)])],
            []);
        var start = new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.FromHours(3));

        Assert.True(AppointmentRules.IsWithinAvailability(schedule, start, start.AddHours(2)));
        Assert.False(AppointmentRules.IsWithinAvailability(schedule, start, start.AddHours(8)));
        Assert.False(AppointmentRules.IsWithinAvailability(schedule, start.AddHours(-2), start));
    }

    [Fact]
    public void A_date_exception_overrides_weekly_hours_and_an_unconfigured_schedule_is_unrestricted()
    {
        var date = new DateOnly(2026, 12, 1);
        var weekly = new WeeklyAvailabilityDay(2, [new AvailabilityWindow(9 * 60, 17 * 60)]);
        var closed = new ProviderAvailabilitySchedule(
            [weekly],
            [new AvailabilityDateException(date, true, [])]);
        var open = new ProviderAvailabilitySchedule(
            [weekly],
            [new AvailabilityDateException(date, false, [new AvailabilityWindow(18 * 60, 20 * 60)])]);
        var afterHours = new DateTimeOffset(2026, 12, 1, 18, 30, 0, TimeSpan.FromHours(3));

        Assert.False(AppointmentRules.IsWithinAvailability(closed, afterHours, afterHours.AddMinutes(30)));
        Assert.True(AppointmentRules.IsWithinAvailability(open, afterHours, afterHours.AddMinutes(30)));
        Assert.True(AppointmentRules.IsWithinAvailability(null, afterHours, afterHours.AddMinutes(30)));
    }
}