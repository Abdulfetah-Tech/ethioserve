using EthioServe.Domain.Common;
using EthioServe.Domain.Enums;

namespace EthioServe.Domain.Entities;

public sealed class ProviderProfile : AuditableEntity
{
    public const int MaxYearsOfExperience = 60;

    private const int MaxNoteLength = 500;

    private ProviderProfile() { }

    public ProviderProfile(
        Guid userId, string businessName, string phoneNumber, int regionId, int cityId,
        int? subCityId = null, string description = "", string address = "", int yearsOfExperience = 0)
    {
        ValidateExperience(yearsOfExperience);

        UserId = userId;
        BusinessName = Guard.NotBlank(businessName, nameof(businessName));
        PhoneNumber = Guard.NotBlank(phoneNumber, nameof(phoneNumber));
        RegionId = regionId;
        CityId = cityId;
        SubCityId = subCityId;
        Description = description;
        Address = address;
        YearsOfExperience = yearsOfExperience;
        VerificationStatus = VerificationStatus.Unverified;
        IsAvailable = true;
    }

    public Guid UserId { get; private set; }

    public string BusinessName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string? ProfileImageKey { get; private set; }

    public int YearsOfExperience { get; private set; }

    public VerificationStatus VerificationStatus { get; private set; }

    /// <summary>Why the provider was rejected or suspended. Shown to the provider; cleared on approval.</summary>
    public string? VerificationNote { get; private set; }

    public DateTimeOffset? VerificationReviewedAt { get; private set; }

    /// <summary>Denormalized aggregate, recalculated in the same transaction as every review change (Phase 9).</summary>
    public decimal AverageRating { get; private set; }

    public int TotalReviews { get; private set; }

    public int RegionId { get; private set; }

    public int CityId { get; private set; }

    public int? SubCityId { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public bool IsAvailable { get; private set; }

    public ProviderAvailabilitySchedule? AvailabilitySchedule { get; private set; }

    public void UpdateDetails(
        string businessName, string description, string phoneNumber, int yearsOfExperience,
        int regionId, int cityId, int? subCityId, string address, decimal? latitude, decimal? longitude)
    {
        ValidateExperience(yearsOfExperience);
        ValidateCoordinates(latitude, longitude);

        BusinessName = Guard.NotBlank(businessName, nameof(businessName));
        PhoneNumber = Guard.NotBlank(phoneNumber, nameof(phoneNumber));
        Description = description.Trim();
        Address = address.Trim();
        YearsOfExperience = yearsOfExperience;
        RegionId = regionId;
        CityId = cityId;
        SubCityId = subCityId;
        Latitude = latitude;
        Longitude = longitude;
    }

    public void SetAvailabilitySchedule(ProviderAvailabilitySchedule? schedule) => AvailabilitySchedule = schedule;

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            throw new DomainException("Latitude and longitude must be given together.");
        }

        if (latitude.HasValue && (latitude.Value < -90m || latitude.Value > 90m))
        {
            throw new DomainException("Latitude must be between -90 and 90.");
        }

        if (longitude.HasValue && (longitude.Value < -180m || longitude.Value > 180m))
        {
            throw new DomainException("Longitude must be between -180 and 180.");
        }
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;
    /// <summary>
    /// Replaces the denormalized rating summary. The review workflow (Phase 9) recalculates it after every review
    /// change; development demo data sets it directly.
    /// </summary>
    public void ApplyRatingSummary(decimal averageRating, int totalReviews)
    {
        if (averageRating < 0m || averageRating > 5m)
        {
            throw new DomainException("averageRating must be between 0 and 5.");
        }

        if (totalReviews < 0)
        {
            throw new DomainException("totalReviews cannot be negative.");
        }

        if (totalReviews == 0 && averageRating != 0m)
        {
            throw new DomainException("A provider without reviews cannot have a rating.");
        }

        AverageRating = Math.Round(averageRating, 2);
        TotalReviews = totalReviews;
    }
    /// <summary>The provider asks to be reviewed. Allowed for new providers and after a rejection.</summary>
    public void RequestVerification()
    {
        if (VerificationStatus is not (VerificationStatus.Unverified or VerificationStatus.Rejected))
        {
            throw new DomainException(
                $"A provider whose verification status is {VerificationStatus} cannot request verification.");
        }

        VerificationStatus = VerificationStatus.Pending;
        VerificationNote = null;
    }

    public void Approve(DateTimeOffset now)
    {
        RequireStatus(VerificationStatus.Pending, "approved");

        VerificationStatus = VerificationStatus.Verified;
        VerificationNote = null;
        VerificationReviewedAt = now;
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        RequireStatus(VerificationStatus.Pending, "rejected");

        VerificationNote = Note(reason);
        VerificationStatus = VerificationStatus.Rejected;
        VerificationReviewedAt = now;
    }

    public void Suspend(string reason, DateTimeOffset now)
    {
        RequireStatus(VerificationStatus.Verified, "suspended");

        VerificationNote = Note(reason);
        VerificationStatus = VerificationStatus.Suspended;
        VerificationReviewedAt = now;
    }

    public void Reinstate(DateTimeOffset now)
    {
        RequireStatus(VerificationStatus.Suspended, "reinstated");

        VerificationStatus = VerificationStatus.Verified;
        VerificationNote = null;
        VerificationReviewedAt = now;
    }

    private void RequireStatus(VerificationStatus expected, string action)
    {
        if (VerificationStatus != expected)
        {
            throw new DomainException($"A provider whose verification status is {VerificationStatus} cannot be {action}.");
        }
    }

    private static void ValidateExperience(int years)
    {
        if (years < 0 || years > MaxYearsOfExperience)
        {
            throw new DomainException($"yearsOfExperience must be between 0 and {MaxYearsOfExperience}.");
        }
    }

    private static string Note(string reason)
    {
        var note = Guard.NotBlank(reason, nameof(reason));

        return note.Length > MaxNoteLength
            ? throw new DomainException($"The note cannot be longer than {MaxNoteLength} characters.")
            : note;
    }
}