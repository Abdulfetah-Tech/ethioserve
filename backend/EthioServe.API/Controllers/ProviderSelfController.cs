using EthioServe.API.Authorization;
using EthioServe.Application.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EthioServe.API.Controllers;

/// <summary>A provider managing their own profile. There is no provider id in any route: it comes from the token.</summary>
[ApiController]
[Authorize(Policy = Policies.ProviderOnly)]
[Route("api/providers/me")]
public sealed class ProviderSelfController(IProviderSelfService providers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProviderProfileDto>> Profile(CancellationToken ct) =>
        Ok(await providers.GetMyProfileAsync(ct));

    [HttpPut]
    public async Task<ActionResult<ProviderProfileDto>> UpdateProfile(UpdateProviderProfileRequest request, CancellationToken ct) =>
        Ok(await providers.UpdateMyProfileAsync(request, ct));

    [HttpPut("availability")]
    public async Task<ActionResult<ProviderProfileDto>> SetAvailability(SetAvailabilityRequest request, CancellationToken ct) =>
        Ok(await providers.SetAvailabilityAsync(request.IsAvailable, ct));

    [HttpGet("availability-calendar")]
    public async Task<ActionResult<ProviderAvailabilityDto>> AvailabilityCalendar(CancellationToken ct) =>
        Ok(await providers.GetAvailabilityCalendarAsync(ct));

    [HttpPut("availability-calendar")]
    public async Task<ActionResult<ProviderAvailabilityDto>> UpdateAvailabilityCalendar(
        UpdateProviderAvailabilityRequest request, CancellationToken ct) =>
        Ok(await providers.UpdateAvailabilityCalendarAsync(request, ct));

    [HttpDelete("availability-calendar")]
    public async Task<IActionResult> ClearAvailabilityCalendar(CancellationToken ct)
    {
        await providers.ClearAvailabilityCalendarAsync(ct);

        return NoContent();
    }

    [HttpPost("verification-request")]
    public async Task<ActionResult<ProviderProfileDto>> RequestVerification(CancellationToken ct) =>
        Ok(await providers.RequestVerificationAsync(ct));

    [HttpGet("services")]
    public async Task<ActionResult<IReadOnlyList<ProviderOfferDto>>> Offers(CancellationToken ct) =>
        Ok(await providers.GetMyOffersAsync(ct));

    [HttpPost("services")]
    public async Task<ActionResult<ProviderOfferDto>> AddOffer(AddOfferRequest request, CancellationToken ct)
    {
        var created = await providers.AddOfferAsync(request, ct);

        return Created($"/api/providers/me/services/{created.Id}", created);
    }

    [HttpPut("services/{id:guid}")]
    public async Task<ActionResult<ProviderOfferDto>> UpdateOffer(Guid id, UpdateOfferRequest request, CancellationToken ct) =>
        Ok(await providers.UpdateOfferAsync(id, request, ct));

    [HttpDelete("services/{id:guid}")]
    public async Task<IActionResult> RemoveOffer(Guid id, CancellationToken ct)
    {
        await providers.RemoveOfferAsync(id, ct);

        return NoContent();
    }
}