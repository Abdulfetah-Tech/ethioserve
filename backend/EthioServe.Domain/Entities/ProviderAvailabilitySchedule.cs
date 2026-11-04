namespace EthioServe.Domain.Entities;

public sealed record AvailabilityWindow(int StartMinute, int EndMinute);

public sealed record WeeklyAvailabilityDay(int DayOfWeek, IReadOnlyList<AvailabilityWindow> Windows);

public sealed record AvailabilityDateException(
    DateOnly Date,
    bool IsClosed,
    IReadOnlyList<AvailabilityWindow> Windows);

public sealed record ProviderAvailabilitySchedule(
    IReadOnlyList<WeeklyAvailabilityDay> WeeklyHours,
    IReadOnlyList<AvailabilityDateException> DateExceptions);
