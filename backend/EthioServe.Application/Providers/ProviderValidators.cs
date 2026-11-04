using EthioServe.Application.Auth;
using EthioServe.Domain.Entities;
using FluentValidation;

namespace EthioServe.Application.Providers;

public sealed class UpdateProviderProfileRequestValidator : AbstractValidator<UpdateProviderProfileRequest>
{
    public UpdateProviderProfileRequestValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.PhoneNumber).ValidPhone();
        RuleFor(x => x.YearsOfExperience).InclusiveBetween(0, ProviderProfile.MaxYearsOfExperience);
        RuleFor(x => x.RegionId).GreaterThan(0);
        RuleFor(x => x.CityId).GreaterThan(0);
        RuleFor(x => x.SubCityId).GreaterThan(0).When(x => x.SubCityId.HasValue);
        RuleFor(x => x.Address).NotNull().MaximumLength(300);

        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).PrecisionScale(9, 6, true).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).PrecisionScale(9, 6, true).When(x => x.Longitude.HasValue);
        RuleFor(x => x.Longitude)
            .Must((request, longitude) => request.Latitude.HasValue == longitude.HasValue)
            .WithMessage("Latitude and longitude must be given together.");
    }
}

public sealed class UpdateProviderAvailabilityRequestValidator : AbstractValidator<UpdateProviderAvailabilityRequest>
{
    private const int MaxWindowsPerDay = 4;
    private const int MaxExceptions = 365;

    public UpdateProviderAvailabilityRequestValidator()
    {
        RuleFor(x => x.WeeklyHours)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(days => days.Count <= 7)
            .WithMessage("Provide no more than one schedule for each day of the week.");
        RuleFor(x => x.WeeklyHours)
            .Must(HaveUniqueWeekdays)
            .When(x => x.WeeklyHours is not null)
            .WithMessage("Each day of the week can appear only once.");
        RuleForEach(x => x.WeeklyHours).ChildRules(day =>
        {
            day.RuleFor(x => x.DayOfWeek).InclusiveBetween(0, 6);
            day.RuleFor(x => x.Windows)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(HaveValidWindows)
                .WithMessage("Use up to four non-overlapping time ranges between 00:00 and 23:59.");
        });

        RuleFor(x => x.DateExceptions)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(exceptions => exceptions.Count <= MaxExceptions)
            .WithMessage($"Provide no more than {MaxExceptions} date exceptions.");
        RuleFor(x => x.DateExceptions)
            .Must(HaveUniqueDates)
            .When(x => x.DateExceptions is not null)
            .WithMessage("Each date can have only one exception.");
        RuleForEach(x => x.DateExceptions).ChildRules(exception =>
        {
            exception.RuleFor(x => x.Windows)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must((model, windows) =>
                    model.IsClosed ? windows.Count == 0 : windows.Count is > 0 and <= MaxWindowsPerDay && HaveValidWindows(windows))
                .WithMessage("A closed date cannot have time ranges; an open date needs up to four valid ranges.");
        });
    }

    private static bool HaveUniqueWeekdays(IReadOnlyList<WeeklyAvailabilityDayRequest> days) =>
        days.Select(day => day.DayOfWeek).Distinct().Count() == days.Count;

    private static bool HaveUniqueDates(IReadOnlyList<AvailabilityDateExceptionRequest> exceptions) =>
        exceptions.Select(exception => exception.Date).Distinct().Count() == exceptions.Count;

    private static bool HaveValidWindows(IReadOnlyList<AvailabilityWindowRequest> windows)
    {
        if (windows.Count > MaxWindowsPerDay)
        {
            return false;
        }

        var ordered = windows.OrderBy(window => window.StartMinute).ToArray();

        for (var index = 0; index < ordered.Length; index++)
        {
            var window = ordered[index];
            if (window.StartMinute < 0 || window.StartMinute >= 1439
                || window.EndMinute <= window.StartMinute || window.EndMinute > 1439)
            {
                return false;
            }

            if (index > 0 && ordered[index - 1].EndMinute > window.StartMinute)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class AddOfferRequestValidator : AbstractValidator<AddOfferRequest>
{
    public AddOfferRequestValidator()
    {
        RuleFor(x => x.ServiceId).NotEmpty();
        RuleFor(x => x.Price).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateOfferRequestValidator : AbstractValidator<UpdateOfferRequest>
{
    public UpdateOfferRequestValidator()
    {
        RuleFor(x => x.Price).InclusiveBetween(0m, 10_000_000m).PrecisionScale(12, 2, true);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}