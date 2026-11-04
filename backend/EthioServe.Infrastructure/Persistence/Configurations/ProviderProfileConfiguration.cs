using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using EthioServe.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace EthioServe.Infrastructure.Persistence.Configurations;

public sealed class ProviderProfileConfiguration : IEntityTypeConfiguration<ProviderProfile>
{
    private static readonly JsonSerializerOptions AvailabilityJsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<ProviderProfile> builder)
    {
        builder.ToTable("provider_profiles", t =>
        {
            t.HasCheckConstraint("ck_provider_profiles_experience", "years_of_experience >= 0");
            t.HasCheckConstraint("ck_provider_profiles_rating", "average_rating >= 0 AND average_rating <= 5");
            t.HasCheckConstraint("ck_provider_profiles_total_reviews", "total_reviews >= 0");
            t.HasCheckConstraint("ck_provider_profiles_verification_status",
                ConfigurationExtensions.InList<VerificationStatus>("verification_status"));
        });
        builder.HasKey(p => p.Id);

        builder.Property(p => p.BusinessName).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(2000).IsRequired();
        builder.Property(p => p.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(p => p.ProfileImageKey).HasMaxLength(300);
        builder.Property(p => p.VerificationStatus).AsStringColumn(20);
        builder.Property(p => p.VerificationNote).HasMaxLength(500);
        builder.Property(p => p.AverageRating).HasPrecision(3, 2);
        builder.Property(p => p.Address).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Latitude).HasPrecision(9, 6);
        builder.Property(p => p.Longitude).HasPrecision(9, 6);
        builder.Property(p => p.AvailabilitySchedule)
            .HasColumnType("jsonb")
            .HasConversion(
                schedule => JsonSerializer.Serialize(schedule, AvailabilityJsonOptions),
                json => JsonSerializer.Deserialize<ProviderAvailabilitySchedule>(json, AvailabilityJsonOptions)!);

        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<ProviderProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Region>().WithMany().HasForeignKey(p => p.RegionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<City>().WithMany().HasForeignKey(p => p.CityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubCity>().WithMany().HasForeignKey(p => p.SubCityId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.UserId).IsUnique().HasDatabaseName("ux_provider_profiles_user_id");
        builder.HasIndex(p => new { p.CityId, p.VerificationStatus, p.IsAvailable })
            .HasDatabaseName("ix_provider_profiles_search");
        builder.HasIndex(p => p.AverageRating).IsDescending().HasDatabaseName("ix_provider_profiles_rating");
    }
}