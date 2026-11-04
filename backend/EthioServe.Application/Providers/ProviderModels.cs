using EthioServe.Domain.Enums;

namespace EthioServe.Application.Providers;

public sealed record ProviderProfileDto(
    Guid Id,
    string BusinessName,
    string Description,
    string PhoneNumber,
    int YearsOfExperience,
    VerificationStatus VerificationStatus,
    string? VerificationNote,
    decimal AverageRating,
    int TotalReviews,
    int RegionId,
    string RegionName,
    int CityId,
    string CityName,
    int? SubCityId,
    string? SubCityName,
    string Address,
    decimal? Latitude,
    decimal? Longitude,
    bool IsAvailable,
    int ActiveServiceCount);

public sealed record ProviderOfferDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    Guid CategoryId,
    string CategoryName,
    decimal Price,
    string? Description,
    bool IsActive);

// Request bodies hold only what a provider may change: no id, status, rating or owner.
public sealed record UpdateProviderProfileRequest(
    string BusinessName,
    string Description,
    string PhoneNumber,
    int YearsOfExperience,
    int RegionId,
    int CityId,
    int? SubCityId,
    string Address,
    decimal? Latitude,
    decimal? Longitude);

public sealed record SetAvailabilityRequest(bool IsAvailable);

public sealed record AvailabilityWindowRequest(int StartMinute, int EndMinute);

public sealed record WeeklyAvailabilityDayRequest(int DayOfWeek, IReadOnlyList<AvailabilityWindowRequest> Windows);

public sealed record AvailabilityDateExceptionRequest(
    DateOnly Date,
    bool IsClosed,
    IReadOnlyList<AvailabilityWindowRequest> Windows);

public sealed record UpdateProviderAvailabilityRequest(
    IReadOnlyList<WeeklyAvailabilityDayRequest> WeeklyHours,
    IReadOnlyList<AvailabilityDateExceptionRequest> DateExceptions);

public sealed record AvailabilityWindowDto(int StartMinute, int EndMinute);

public sealed record WeeklyAvailabilityDayDto(int DayOfWeek, IReadOnlyList<AvailabilityWindowDto> Windows);

public sealed record AvailabilityDateExceptionDto(
    DateOnly Date,
    bool IsClosed,
    IReadOnlyList<AvailabilityWindowDto> Windows);

public sealed record ProviderAvailabilityDto(
    bool IsConfigured,
    IReadOnlyList<WeeklyAvailabilityDayDto> WeeklyHours,
    IReadOnlyList<AvailabilityDateExceptionDto> DateExceptions);

public sealed record AddOfferRequest(Guid ServiceId, decimal Price, string? Description);

public sealed record UpdateOfferRequest(decimal Price, string? Description, bool IsActive);

/// <summary>Everything a provider can do with their own profile. The provider is always the signed-in user.</summary>
public interface IProviderSelfService
{
    Task<ProviderProfileDto> GetMyProfileAsync(CancellationToken ct = default);

    Task<ProviderProfileDto> UpdateMyProfileAsync(UpdateProviderProfileRequest request, CancellationToken ct = default);

    Task<ProviderProfileDto> SetAvailabilityAsync(bool isAvailable, CancellationToken ct = default);

    Task<ProviderAvailabilityDto> GetAvailabilityCalendarAsync(CancellationToken ct = default);

    Task<ProviderAvailabilityDto> UpdateAvailabilityCalendarAsync(
        UpdateProviderAvailabilityRequest request, CancellationToken ct = default);

    Task ClearAvailabilityCalendarAsync(CancellationToken ct = default);

    Task<ProviderProfileDto> RequestVerificationAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ProviderOfferDto>> GetMyOffersAsync(CancellationToken ct = default);

    Task<ProviderOfferDto> AddOfferAsync(AddOfferRequest request, CancellationToken ct = default);

    Task<ProviderOfferDto> UpdateOfferAsync(Guid offerId, UpdateOfferRequest request, CancellationToken ct = default);

    Task RemoveOfferAsync(Guid offerId, CancellationToken ct = default);
}