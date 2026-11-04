using System.Globalization;
using EthioServe.Domain.Entities;

namespace EthioServe.Application.Appointments;

/// <summary>The time rules for appointments, kept in one place so they can be tested without a database.</summary>
public static class AppointmentRules
{
    public static readonly TimeSpan MinLead = TimeSpan.FromHours(1);

    public static readonly TimeSpan MaxAdvance = TimeSpan.FromDays(90);

    /// <summary>A provider can start the job this long before the agreed time.</summary>
    public static readonly TimeSpan EarlyStartWindow = TimeSpan.FromHours(2);

    // Ethiopia is UTC+3 all year (no daylight saving).
    private static readonly TimeSpan AddisOffset = TimeSpan.FromHours(3);

    /// <summary>
    /// UTC, cut to the minute. PostgreSQL only accepts UTC for timestamps with time zone, so a time sent as
    /// "+03:00" must be converted before it is stored.
    /// </summary>
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();

        return new DateTimeOffset(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMinute), TimeSpan.Zero);
    }

    public static string? StartProblem(DateTimeOffset start, DateTimeOffset now)
    {
        if (start < now + MinLead)
        {
            return "Choose a time at least 1 hour from now.";
        }

        if (start > now + MaxAdvance)
        {
            return "Choose a time within the next 90 days.";
        }

        return null;
    }

    public static DateTimeOffset EndFor(DateTimeOffset start, int durationMinutes) => start.AddMinutes(durationMinutes);

    public static bool CanStartWork(DateTimeOffset start, DateTimeOffset now) => now >= start - EarlyStartWindow;

    /// <summary>Two periods overlap when each starts before the other ends. Touching periods do not overlap.</summary>
    public static bool Overlaps(DateTimeOffset startA, DateTimeOffset endA, DateTimeOffset startB, DateTimeOffset endB) =>
        startA < endB && startB < endA;

    public static bool IsWithinAvailability(
        ProviderAvailabilitySchedule? schedule, DateTimeOffset start, DateTimeOffset end)
    {
        if (schedule is null)
        {
            return true;
        }

        var localStart = start.UtcDateTime.Add(AddisOffset);
        var localEnd = end.UtcDateTime.Add(AddisOffset);
        if (localStart.Date != localEnd.Date)
        {
            return false;
        }

        var date = DateOnly.FromDateTime(localStart);
        var startMinute = localStart.Hour * 60 + localStart.Minute;
        var endMinute = localEnd.Hour * 60 + localEnd.Minute;
        var exception = schedule.DateExceptions.FirstOrDefault(item => item.Date == date);
        if (exception is not null)
        {
            return !exception.IsClosed && Fits(exception.Windows, startMinute, endMinute);
        }

        var weekday = schedule.WeeklyHours.FirstOrDefault(item => item.DayOfWeek == (int)localStart.DayOfWeek);

        return weekday is not null && Fits(weekday.Windows, startMinute, endMinute);
    }

    /// <summary>For messages: the time as people in Addis Ababa read it, for example "1 Dec 2026, 14:00".</summary>
    public static string FormatLocal(DateTimeOffset value) =>
        value.UtcDateTime.Add(AddisOffset).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);

    private static bool Fits(IReadOnlyList<AvailabilityWindow> windows, int startMinute, int endMinute) =>
        windows.Any(window => window.StartMinute <= startMinute && endMinute <= window.EndMinute);
}