using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>Payments for accepted quotes. Only the customer can create payments; both parties can view them.</summary>
[ApiController]
[Authorize(Policy = Policies.CustomerOnly)]
[Route("api/requests/{requestId:guid}/payments")]
public sealed class PaymentsController(ICustomerPaymentService payments) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Start(
        Guid requestId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await payments.StartAsync(requestId, idempotencyKey, ct);

        return result.Created
            ? CreatedAtAction(nameof(Get), new { requestId, id = result.Payment.Id }, result.Payment)
            : Ok(result.Payment);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentDto>>> List(
        Guid requestId, [FromQuery] PaymentQuery query, CancellationToken ct)
    {
        query.RequestId = requestId;
        return Ok(await payments.ListAsync(query, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> Get(Guid requestId, Guid id, CancellationToken ct)
    {
        var payment = await payments.GetAsync(id, ct);
        return payment.RequestId == requestId ? Ok(payment) : NotFound();
    }
}
