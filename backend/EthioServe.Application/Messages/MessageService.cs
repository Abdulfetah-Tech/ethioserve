using EthioServe.Application.Abstractions;
using EthioServe.Application.Common;
using EthioServe.Application.Common.Exceptions;
using EthioServe.Application.Notifications;
using EthioServe.Domain.Entities;
using EthioServe.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EthioServe.Application.Messages;

public sealed class MessageService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    IAuditTrail audit,
    INotifier notifier,
    IValidator<SendMessageRequest> validator) : IMessageService
{
    private sealed record Participants(
        Guid CustomerId,
        Guid ProviderId,
        Guid CustomerUserId,
        Guid ProviderUserId);

    private Guid UserId =>
        currentUser.UserId ?? throw new ForbiddenException("You must be signed in.");

    public async Task<PagedResult<MessageDto>> ListAsync(
        Guid requestId, Guid providerId, MessageQuery query, CancellationToken ct = default)
    {
        _ = await FindParticipantsAsync(requestId, providerId, ct)
            ?? throw new NotFoundException("Request not found.");

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);

        var rows = db.RequestMessages.AsNoTracking()
            .Where(m => m.ServiceRequestId == requestId && m.ProviderId == providerId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id);

        var total = await rows.CountAsync(ct);

        var items = await rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageDto(
                m.Id, m.ServiceRequestId, m.ProviderId, m.SenderUserId, m.Body, m.CreatedAt))
            .ToListAsync(ct);

        return new PagedResult<MessageDto>(items, page, pageSize, total);
    }

    public async Task<MessageDto> SendAsync(
        Guid requestId, Guid providerId, SendMessageRequest request, CancellationToken ct = default)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var senderId = UserId;

        if (request.ProviderId != providerId)
        {
            throw new NotFoundException("Request not found.");
        }

        var participants = await FindParticipantsAsync(requestId, providerId, ct)
            ?? throw new NotFoundException("Request not found.");

        var message = new RequestMessage(
            requestId, providerId, senderId, request.Body);

        db.RequestMessages.Add(message);
        audit.Record(
            "MessageSent", nameof(RequestMessage), message.Id, null,
            new { message.ServiceRequestId, message.ProviderId, message.SenderUserId });
        await db.SaveChangesAsync(ct);

        var recipientUser = currentUser.CustomerId == participants.CustomerId
            ? participants.ProviderUserId
            : participants.CustomerUserId;

        await notifier.ToUserAsync(
            recipientUser, NotificationType.NewMessage, "New message",
            "You have a new message on a service request.", "ServiceRequest", requestId, ct);
        await db.SaveChangesAsync(ct);

        return new MessageDto(
            message.Id, message.ServiceRequestId, message.ProviderId, message.SenderUserId,
            message.Body, message.CreatedAt);
    }

    private async Task<Participants?> FindParticipantsAsync(Guid requestId, Guid providerId, CancellationToken ct)
    {
        var customerId = currentUser.CustomerId;
        var currentProviderId = currentUser.ProviderId;
        if (customerId is null && currentProviderId != providerId)
        {
            return null;
        }

        return await (
                from r in db.ServiceRequests.AsNoTracking()
                join q in db.Quotes.AsNoTracking() on r.Id equals q.ServiceRequestId
                join c in db.CustomerProfiles.AsNoTracking() on r.CustomerId equals c.Id
                join p in db.ProviderProfiles.AsNoTracking() on q.ProviderId equals p.Id
                where r.Id == requestId
                      && q.ProviderId == providerId
                      && (customerId == null || r.CustomerId == customerId)
                      && (currentProviderId == null || q.ProviderId == currentProviderId)
                select new Participants(r.CustomerId, q.ProviderId, c.UserId, p.UserId))
            .FirstOrDefaultAsync(ct) is { } row
            ? row
            : null;
    }
}
