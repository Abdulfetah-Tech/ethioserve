using System.Linq.Expressions;
using EthioServe.Application.Abstractions;
using EthioServe.Application.Appointments;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Providers;

public sealed class ProviderSelfService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    IValidator<UpdateProviderProfileRequest> profileValidator,
    IValidator<UpdateProviderAvailabilityRequest> availabilityValidator,
    IValidator<AddOfferRequest> addOfferValidator,
    IValidator<UpdateOfferRequest> updateOfferValidator) : IProviderSelfService
{
    private const int MinDescriptionLengthForVerification = 20;

    private sealed class OfferRow
    {
        public ProviderService Offer { get; set; } = null!;

        public string ServiceName { get; set; } = string.Empty;

        public Guid CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;
    }

    private static readonly Expression<Func<OfferRow, ProviderOfferDto>> ToOfferDto = x => new ProviderOfferDto(
        x.Offer.Id,
        x.Offer.ServiceId,
        x.ServiceName,
        x.CategoryId,
        x.CategoryName,
        x.Offer.Price,
        x.Offer.Description,
        x.Offer.IsActive);

    // The provider comes from the signed-in user's token, never from the request.
    private Guid ProviderId =>
        currentUser.ProviderId ?? throw new ForbiddenException("This account has no provider profile.");

    // ---------------------------------------------------------------- profile

    public async Task<ProviderProfileDto> GetMyProfileAsync(CancellationToken ct = default)
    {
        var providerId = ProviderId;

        return await ProfileDtos(db.ProviderProfiles.AsNoTracking().Where(p => p.Id == providerId))
                   .FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Provider profile not found.");
    }

    public async Task<ProviderProfileDto> UpdateMyProfileAsync(UpdateProviderProfileRequest request, CancellationToken ct = default)
    {
        await profileValidator.ValidateAndThrowAsync(request, ct);

        var profile = await FindProfileAsync(ct);
        await EnsureLocationIsConsistentAsync(request.RegionId, request.CityId, request.SubCityId, ct);

        profile.UpdateDetails(
            request.BusinessName, request.Description, request.PhoneNumber, request.YearsOfExperience,
            request.RegionId, request.CityId, request.SubCityId, request.Address, request.Latitude, request.Longitude);

        await db.SaveChangesAsync(ct);

        return await GetMyProfileAsync(ct);
    }

    public async Task<ProviderProfileDto> SetAvailabilityAsync(bool isAvailable, CancellationToken ct = default)
    {
        var profile = await FindProfileAsync(ct);

        profile.SetAvailability(isAvailable);
        await db.SaveChangesAsync(ct);

        return await GetMyProfileAsync(ct);
    }

    public async Task<ProviderAvailabilityDto> GetAvailabilityCalendarAsync(CancellationToken ct = default)
    {
        var providerId = ProviderId;
        var profile = await db.ProviderProfiles.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == providerId, ct)
            ?? throw new NotFoundException("Provider profile not found.");

        return profile.AvailabilitySchedule is { } schedule
            ? ToAvailabilityDto(schedule)
            : new ProviderAvailabilityDto(false, [], []);
    }

    public async Task<ProviderAvailabilityDto> UpdateAvailabilityCalendarAsync(
        UpdateProviderAvailabilityRequest request, CancellationToken ct = default)
    {
        await availabilityValidator.ValidateAndThrowAsync(request, ct);

        var providerId = ProviderId;

        return await db.WithExclusiveLockAsync(ProviderScheduleLock.Key(providerId), async () =>
        {
            var profile = await FindProfileAsync(ct);
            var before = profile.AvailabilitySchedule;
            var schedule = new ProviderAvailabilitySchedule(
                request.WeeklyHours.Select(day => new WeeklyAvailabilityDay(
                    day.DayOfWeek,
                    day.Windows.Select(window => new AvailabilityWindow(window.StartMinute, window.EndMinute)).ToArray()))
                .ToArray(),
                request.DateExceptions.Select(exception => new AvailabilityDateException(
                    exception.Date,
                    exception.IsClosed,
                    exception.Windows.Select(window => new AvailabilityWindow(window.StartMinute, window.EndMinute)).ToArray()))
                .ToArray());

            profile.SetAvailabilitySchedule(schedule);
            audit.Record("ProviderAvailabilityCalendarUpdated", nameof(ProviderProfile), profile.Id, before, schedule);
            await db.SaveChangesAsync(ct);

            return ToAvailabilityDto(schedule);
        }, ct);
    }

    public async Task ClearAvailabilityCalendarAsync(CancellationToken ct = default)
    {
        var providerId = ProviderId;

        await db.WithExclusiveLockAsync(ProviderScheduleLock.Key(providerId), async () =>
        {
            var profile = await FindProfileAsync(ct);
            var before = profile.AvailabilitySchedule;
            if (before is not null)
            {
                profile.SetAvailabilitySchedule(null);
                audit.Record(
                    "ProviderAvailabilityCalendarCleared", nameof(ProviderProfile), profile.Id, before, null);
                await db.SaveChangesAsync(ct);
            }

            return true;
        }, ct);
    }

    public async Task<ProviderProfileDto> RequestVerificationAsync(CancellationToken ct = default)
    {
        var profile = await FindProfileAsync(ct);

        var activeOffers = await db.ProviderServices.CountAsync(o => o.ProviderId == profile.Id && o.IsActive, ct);

        var missing = new List<string>();
        if (profile.Description.Length < MinDescriptionLengthForVerification)
        {
            missing.Add($"a description of at least {MinDescriptionLengthForVerification} characters");
        }

        if (string.IsNullOrWhiteSpace(profile.Address))
        {
            missing.Add("an address");
        }

        if (activeOffers == 0)
        {
            missing.Add("at least one service");
        }

        if (missing.Count > 0)
        {
            throw new DomainException($"Before requesting verification, add {string.Join(", ", missing)}.");
        }

        var before = new { profile.VerificationStatus };
        profile.RequestVerification();
        audit.Record(
            "ProviderVerificationRequested", nameof(ProviderProfile), profile.Id, before, new { profile.VerificationStatus });
        await db.SaveChangesAsync(ct);

        return await GetMyProfileAsync(ct);
    }

    // ---------------------------------------------------------------- offers

    public async Task<IReadOnlyList<ProviderOfferDto>> GetMyOffersAsync(CancellationToken ct = default)
    {
        var providerId = ProviderId;

        return await OfferRows()
            .Where(x => x.Offer.ProviderId == providerId)
            .OrderBy(x => x.CategoryName).ThenBy(x => x.ServiceName)
            .Select(ToOfferDto)
            .ToListAsync(ct);
    }

    public async Task<ProviderOfferDto> AddOfferAsync(AddOfferRequest request, CancellationToken ct = default)
    {
        await addOfferValidator.ValidateAndThrowAsync(request, ct);

        var providerId = ProviderId;

        var serviceIsAvailable = await (
            from s in db.Services.AsNoTracking()
            join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
            where s.Id == request.ServiceId && s.IsActive && c.IsActive
            select s.Id).AnyAsync(ct);

        if (!serviceIsAvailable)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.ServiceId), "The selected service does not exist or is not available.")
            });
        }

        if (await db.ProviderServices.AnyAsync(o => o.ProviderId == providerId && o.ServiceId == request.ServiceId, ct))
        {
            throw new ConflictException("You already offer this service.");
        }

        var offer = new ProviderService(providerId, request.ServiceId, request.Price, Clean(request.Description));
        db.ProviderServices.Add(offer);
        await db.SaveChangesAsync(ct);

        return await GetOfferAsync(providerId, offer.Id, ct);
    }

    public async Task<ProviderOfferDto> UpdateOfferAsync(Guid offerId, UpdateOfferRequest request, CancellationToken ct = default)
    {
        await updateOfferValidator.ValidateAndThrowAsync(request, ct);

        var providerId = ProviderId;
        var offer = await FindOfferAsync(providerId, offerId, ct);

        offer.Update(request.Price, Clean(request.Description), request.IsActive);
        await db.SaveChangesAsync(ct);

        return await GetOfferAsync(providerId, offerId, ct);
    }

    public async Task RemoveOfferAsync(Guid offerId, CancellationToken ct = default)
    {
        var offer = await FindOfferAsync(ProviderId, offerId, ct);

        db.ProviderServices.Remove(offer);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- helpers

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<ProviderProfile> FindProfileAsync(CancellationToken ct)
    {
        var providerId = ProviderId;

        return await db.ProviderProfiles.FirstOrDefaultAsync(p => p.Id == providerId, ct)
               ?? throw new NotFoundException("Provider profile not found.");
    }

    // Looks the offer up by id AND owner, so another provider's offer is simply "not found" (no IDOR).
    private async Task<ProviderService> FindOfferAsync(Guid providerId, Guid offerId, CancellationToken ct) =>
        await db.ProviderServices.FirstOrDefaultAsync(o => o.Id == offerId && o.ProviderId == providerId, ct)
        ?? throw new NotFoundException("Service offer not found.");

    private async Task<ProviderOfferDto> GetOfferAsync(Guid providerId, Guid offerId, CancellationToken ct) =>
        await OfferRows()
            .Where(x => x.Offer.Id == offerId && x.Offer.ProviderId == providerId)
            .Select(ToOfferDto)
            .FirstAsync(ct);

    private async Task EnsureLocationIsConsistentAsync(int regionId, int cityId, int? subCityId, CancellationToken ct)
    {
        if (!await db.Cities.AnyAsync(c => c.Id == cityId && c.RegionId == regionId, ct))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("CityId", "The selected city does not belong to the selected region.")
            });
        }

        if (subCityId is { } subId && !await db.SubCities.AnyAsync(s => s.Id == subId && s.CityId == cityId, ct))
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure("SubCityId", "The selected sub-city does not belong to the selected city.")
            });
        }
    }

    private IQueryable<OfferRow> OfferRows() =>
        from o in db.ProviderServices.AsNoTracking()
        join s in db.Services.AsNoTracking() on o.ServiceId equals s.Id
        join c in db.ServiceCategories.AsNoTracking() on s.CategoryId equals c.Id
        select new OfferRow { Offer = o, ServiceName = s.Name, CategoryId = c.Id, CategoryName = c.Name };

    private IQueryable<ProviderProfileDto> ProfileDtos(IQueryable<ProviderProfile> profiles) =>
        profiles.Select(p => new ProviderProfileDto(
            p.Id,
            p.BusinessName,
            p.Description,
            p.PhoneNumber,
            p.YearsOfExperience,
            p.VerificationStatus,
            p.VerificationNote,
            p.AverageRating,
            p.TotalReviews,
            p.RegionId,
            db.Regions.Where(r => r.Id == p.RegionId).Select(r => r.Name).FirstOrDefault() ?? string.Empty,
            p.CityId,
            db.Cities.Where(c => c.Id == p.CityId).Select(c => c.Name).FirstOrDefault() ?? string.Empty,
            p.SubCityId,
            db.SubCities.Where(s => s.Id == p.SubCityId).Select(s => s.Name).FirstOrDefault(),
            p.Address,
            p.Latitude,
            p.Longitude,
            p.IsAvailable,
            db.ProviderServices.Count(o => o.ProviderId == p.Id && o.IsActive)));

    private static ProviderAvailabilityDto ToAvailabilityDto(ProviderAvailabilitySchedule schedule) => new(
        true,
        schedule.WeeklyHours.Select(day => new WeeklyAvailabilityDayDto(
            day.DayOfWeek,
            day.Windows.Select(window => new AvailabilityWindowDto(window.StartMinute, window.EndMinute)).ToArray()))
        .ToArray(),
        schedule.DateExceptions.Select(exception => new AvailabilityDateExceptionDto(
            exception.Date,
            exception.IsClosed,
            exception.Windows.Select(window => new AvailabilityWindowDto(window.StartMinute, window.EndMinute)).ToArray()))
        .ToArray());
}