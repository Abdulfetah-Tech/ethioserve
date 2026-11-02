using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

[ApiController]
[Authorize(Policy = Policies.CustomerOnly)]
[Route("api/customer/payments")]
public sealed class CustomerPaymentsController(ICustomerPaymentService payments) : ControllerBase
{
    [HttpPost("requests/{requestId:guid}")]
    public async Task<ActionResult<PaymentDto>> Start(
        Guid requestId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await payments.StartAsync(requestId, idempotencyKey, ct);

        return result.Created
            ? CreatedAtAction(nameof(Get), new { id = result.Payment.Id }, result.Payment)
            : Ok(result.Payment);
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<PaymentDto>>> List(
        [FromQuery] PaymentQuery query, CancellationToken ct) =>
        Ok(await payments.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await payments.GetAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<PaymentDto>> Cancel(Guid id, CancellationToken ct) =>
        Ok(await payments.CancelAsync(id, ct));

    [HttpPost("{id:guid}/simulate")]
    public async Task<ActionResult<PaymentDto>> Simulate(
        Guid id, SimulatePaymentRequest request, CancellationToken ct) =>
        Ok(await payments.SimulateAsync(id, request, ct));
}
