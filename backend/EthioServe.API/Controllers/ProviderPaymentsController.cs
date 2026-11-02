using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

[ApiController]
[Route("api/provider/payments")]
[Authorize(Policy = Policies.ProviderOnly)]
public sealed class ProviderPaymentsController(IProviderPaymentService payments) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProviderPaymentDto>>> List(
        [FromQuery] ProviderPaymentQuery query, CancellationToken ct) =>
        Ok(await payments.ListAsync(query, ct));

    [HttpGet("earnings")]
    public async Task<ActionResult<EarningsDto>> Earnings(CancellationToken ct) =>
        Ok(await payments.EarningsAsync(ct));
}
