using EthioServe.API.Authorization;
using EthioServe.Application.Common;
using EthioServe.Application.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

[ApiController]
[Authorize(Policy = Policies.AdminOnly)]
[Route("api/admin/reviews")]
public sealed class AdminReviewsController(IReviewService reviews) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminReviewDto>>> List(
        [FromQuery] AdminReviewQuery query, CancellationToken ct) =>
        Ok(await reviews.ListAdminAsync(query, ct));

    [HttpPost("{id:guid}/hide")]
    public async Task<ActionResult<AdminReviewDto>> Hide(Guid id, CancellationToken ct) =>
        Ok(await reviews.HideAsync(id, ct));

    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<AdminReviewDto>> Restore(Guid id, CancellationToken ct) =>
        Ok(await reviews.RestoreAsync(id, ct));
}
