using EthioServe.Application.Common;
using EthioServe.Domain.Entities;

namespace EthioServe.Application.Messages;

public sealed record SendMessageRequest(Guid ProviderId, string Body);

public sealed record MessageDto(
    Guid Id,
    Guid ServiceRequestId,
    Guid ProviderId,
    Guid SenderUserId,
    string Body,
    DateTimeOffset CreatedAt);

/// <summary>Bound from the query string. Paging values are clamped by <see cref="Paging.Normalize"/>.</summary>
public sealed class MessageQuery
{
    public Guid ProviderId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;
}

/// <summary>Messages on a service request. Only the customer and the quoting provider can read or send them.</summary>
public interface IMessageService
{
    Task<PagedResult<MessageDto>> ListAsync(
        Guid requestId, Guid providerId, MessageQuery query, CancellationToken ct = default);

    Task<MessageDto> SendAsync(
        Guid requestId, Guid providerId, SendMessageRequest request, CancellationToken ct = default);
}
