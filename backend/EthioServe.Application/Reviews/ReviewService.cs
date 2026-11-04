using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Common;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Reviews;

public sealed class ReviewService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    IValidator<CreateReviewRequest> validator) : IReviewService
{
    private Guid CustomerId =>
        currentUser.CustomerId ?? throw new ForbiddenException("This account has no customer profile.");

    public async Task<ProviderReviewDto> CreateAsync(
        Guid requestId, CreateReviewRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var customerId = CustomerId;
        var providerId = await FindReviewableProviderIdAsync(requestId, customerId, ct);

        // Rating summaries for one provider change serially, so no completed job can lose another job's rating update.
        return await db.WithExclusiveLockAsync(
            $"provider-rating:{providerId:N}",
            () => CreateCoreAsync(requestId, customerId, providerId, request, ct),
            ct);
    }

    public async Task<PagedResult<ProviderReviewDto>> ListPublicAsync(
        Guid providerId, ProviderReviewQuery query, CancellationToken ct = default)
    {
        var exists = await db.ProviderProfiles.AsNoTracking().AnyAsync(
            p => p.Id == providerId && p.VerificationStatus == VerificationStatus.Verified, ct);

        if (!exists)
        {
            throw new NotFoundException("Provider not found.");
        }

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var reviews = db.Reviews.AsNoTracking()
            .Where(r => r.ProviderId == providerId && r.IsVisible)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        var total = await reviews.CountAsync(ct);
        var items = await reviews.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new ProviderReviewDto(r.Id, r.Rating, r.Comment, r.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<ProviderReviewDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<AdminReviewDto>> ListAdminAsync(AdminReviewQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var reviews = db.Reviews.AsNoTracking();

        if (query.IsVisible is { } isVisible)
        {
            reviews = reviews.Where(review => review.IsVisible == isVisible);
        }

        var total = await reviews.CountAsync(ct);
        var items = await (
                from review in reviews
                join provider in db.ProviderProfiles.AsNoTracking() on review.ProviderId equals provider.Id
                orderby review.CreatedAt descending, review.Id descending
                select new AdminReviewDto(
                    review.Id,
                    provider.BusinessName,
                    review.Rating,
                    review.Comment,
                    review.CreatedAt,
                    review.IsVisible))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AdminReviewDto>(items, page, pageSize, total);
    }

    public Task<AdminReviewDto> HideAsync(Guid reviewId, CancellationToken ct = default) =>
        SetVisibilityAsync(reviewId, isVisible: false, ct);

    public Task<AdminReviewDto> RestoreAsync(Guid reviewId, CancellationToken ct = default) =>
        SetVisibilityAsync(reviewId, isVisible: true, ct);

    private async Task<ProviderReviewDto> CreateCoreAsync(
        Guid requestId, Guid customerId, Guid providerId, CreateReviewRequest request, CancellationToken ct)
    {
        // Recheck inside the lock. A customer can only review their own job after its appointment was completed.
        _ = await FindReviewableProviderIdAsync(requestId, customerId, ct);

        if (await db.Reviews.AnyAsync(r => r.ServiceRequestId == requestId, ct))
        {
            throw new ConflictException("You have already reviewed this completed job.");
        }

        var review = new Review(requestId, customerId, providerId, request.Rating, request.Comment);
        db.Reviews.Add(review);
        await db.SaveChangesAsync(ct);

        var summary = await db.Reviews.AsNoTracking()
            .Where(r => r.ProviderId == providerId && r.IsVisible)
            .GroupBy(_ => 1)
            .Select(group => new { Average = group.Average(r => (decimal)r.Rating), Count = group.Count() })
            .SingleAsync(ct);

        var provider = await db.ProviderProfiles.FirstAsync(p => p.Id == providerId, ct);
        provider.ApplyRatingSummary(summary.Average, summary.Count);

        // Keep feedback text out of the audit trail. It can contain personal information.
        audit.Record(
            "ReviewCreated", nameof(Review), review.Id, null,
            new { review.ServiceRequestId, review.ProviderId, review.Rating });

        await notifier.ToProviderAsync(
            providerId, NotificationType.ReviewReceived, "New customer review",
            $"A customer rated your completed work {review.Rating} out of 5.",
            nameof(Review), review.Id, ct);

        await db.SaveChangesAsync(ct);
        return ToDto(review);
    }

    private async Task<Guid> FindReviewableProviderIdAsync(Guid requestId, Guid customerId, CancellationToken ct)
    {
        var row = await (
                from request in db.ServiceRequests.AsNoTracking()
                join appointment in db.Appointments.AsNoTracking() on request.Id equals appointment.ServiceRequestId
                where request.Id == requestId
                      && request.CustomerId == customerId
                      && request.Status == ServiceRequestStatus.Completed
                      && appointment.Status == AppointmentStatus.Completed
                select new { appointment.ProviderId })
            .SingleOrDefaultAsync(ct);

        if (row is null)
        {
            var owned = await db.ServiceRequests.AsNoTracking()
                .AnyAsync(r => r.Id == requestId && r.CustomerId == customerId, ct);

            if (!owned)
            {
                throw new NotFoundException("Request not found.");
            }

            throw new DomainException("You can review a job only after its appointment is completed.");
        }

        return row.ProviderId;
    }

    private async Task<AdminReviewDto> SetVisibilityAsync(Guid reviewId, bool isVisible, CancellationToken ct)
    {
        var providerId = await db.Reviews.AsNoTracking()
            .Where(review => review.Id == reviewId)
            .Select(review => (Guid?)review.ProviderId)
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Review not found.");

        return await db.WithExclusiveLockAsync(
            $"provider-rating:{providerId:N}",
            async () =>
            {
                var review = await db.Reviews.FirstOrDefaultAsync(item => item.Id == reviewId, ct)
                             ?? throw new NotFoundException("Review not found.");
                var provider = await db.ProviderProfiles.FirstAsync(item => item.Id == providerId, ct);

                if (review.IsVisible != isVisible)
                {
                    var before = Snapshot(review);
                    if (isVisible)
                    {
                        review.Show();
                    }
                    else
                    {
                        review.Hide();
                    }

                    var summary = await db.Reviews.AsNoTracking()
                        .Where(item => item.ProviderId == providerId && item.IsVisible && item.Id != reviewId)
                        .GroupBy(_ => 1)
                        .Select(group => new
                        {
                            RatingSum = group.Sum(item => (decimal)item.Rating),
                            TotalReviews = group.Count()
                        })
                        .SingleOrDefaultAsync(ct);

                    var totalReviews = (summary?.TotalReviews ?? 0) + (isVisible ? 1 : 0);
                    var ratingSum = (summary?.RatingSum ?? 0m) + (isVisible ? review.Rating : 0);
                    var averageRating = totalReviews == 0 ? 0m : ratingSum / totalReviews;
                    provider.ApplyRatingSummary(averageRating, totalReviews);
                    audit.Record(
                        isVisible ? "ReviewRestored" : "ReviewHidden",
                        nameof(Review),
                        review.Id,
                        before,
                        Snapshot(review));
                    await db.SaveChangesAsync(ct);
                }

                return ToAdminDto(review, provider.BusinessName);
            },
            ct);
    }

    private static object Snapshot(Review review) => new
    {
        review.ProviderId,
        review.Rating,
        review.IsVisible
    };

    private static ProviderReviewDto ToDto(Review review) =>
        new(review.Id, review.Rating, review.Comment, review.CreatedAt);

    private static AdminReviewDto ToAdminDto(Review review, string providerBusinessName) =>
        new(review.Id, providerBusinessName, review.Rating, review.Comment, review.CreatedAt, review.IsVisible);
}
