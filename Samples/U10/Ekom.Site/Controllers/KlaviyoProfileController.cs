using Ekom.Klaviyo.Services;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;

namespace Ekom.Site.Controllers;

public class KlaviyoProfileController : UmbracoAuthorizedApiController
{
    private readonly IKlaviyoProfilesService _profilesService;

    public KlaviyoProfileController(IKlaviyoProfilesService profilesService)
    {
        _profilesService = profilesService;
    }


    public async Task<IActionResult> GetById(
        string profileId,
        [FromQuery] string? storeAlias = null,
        [FromQuery] bool includeSubscriptions = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return BadRequest("Missing profileId.");

        var result = await _profilesService.GetProfileByIdAsync(profileId, storeAlias, includeSubscriptions, ct);
        return result is null ? NotFound() : Ok(result);
    }

    public async Task<IActionResult> GetByEmail(
        [FromQuery] string? email,
        [FromQuery] string? storeAlias = null,
        [FromQuery] bool includeSubscriptions = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Missing email.");

        var result = await _profilesService.GetProfileByEmailAsync(email, storeAlias, includeSubscriptions, ct);
        return result is null ? NotFound() : Ok(result);
    }


    public async Task<IActionResult> GetListIdsById(
        string profileId,
        [FromQuery] string? storeAlias = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return BadRequest("Missing profileId.");

        var listIds = await _profilesService.GetProfileListIdsAsync(profileId, storeAlias, ct);
        return listIds is null ? NotFound() : Ok(listIds);
    }

    public async Task<IActionResult> GetListIdsByEmail(
        [FromQuery] string? email,
        [FromQuery] string? storeAlias = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Missing email.");

        var listIds = await _profilesService.GetProfileListIdsByEmailAsync(email, storeAlias, ct);
        return listIds is null ? NotFound() : Ok(listIds);
    }
}
