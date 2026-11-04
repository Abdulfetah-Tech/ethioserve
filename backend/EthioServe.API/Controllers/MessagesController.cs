using EthioServe.Application.Common;
using EthioServe.Application.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Customer-provider messaging on a service request. Only the two participants can read or send messages.</summary>
[ApiController]
[Authorize]
[Route("api/requests/{requestId:guid}/messages")]
public sealed class MessagesController(IMessageService messages) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<MessageDto>>> List(
        Guid requestId, [FromQuery] MessageQuery query, CancellationToken ct) =>
        Ok(await messages.ListAsync(requestId, query.ProviderId, query, ct));

    [HttpPost]
    public async Task<ActionResult<MessageDto>> Send(
        Guid requestId, SendMessageRequest request, CancellationToken ct) =>
        Ok(await messages.SendAsync(requestId, request.ProviderId, request, ct));
}
