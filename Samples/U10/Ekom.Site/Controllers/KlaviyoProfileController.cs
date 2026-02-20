using Ekom.Klaviyo.Models.Profiles;
using Ekom.Klaviyo.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Site.Controllers;

[ApiController]
[Route("ekom/klaviyo/profile")]
public class KlaviyoProfileController : ControllerBase
{
    private readonly IKlaviyoProfilesService _profilesService;

    public KlaviyoProfileController(
        IKlaviyoProfilesService profilesService)
    {
        _profilesService = profilesService;
    }

    [HttpGet("by-id")]
    public async Task<IActionResult> GetByIdAsync(
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

    [HttpGet("by-email")]
    public async Task<IActionResult> GetByEmailAsync(
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


    [HttpGet("list-ids/by-id")]
    public async Task<IActionResult> GetListIdsByIdAsync(
        string profileId,
        [FromQuery] string? storeAlias = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profileId))
            return BadRequest("Missing profileId.");

        var listIds = await _profilesService.GetProfileListIdsAsync(profileId, storeAlias, ct);
        return listIds is null ? NotFound() : Ok(listIds);
    }

    [HttpGet("list-ids/by-email")]
    public async Task<IActionResult> GetListIdsByEmailAsync(
        [FromQuery] string? email,
        [FromQuery] string? storeAlias = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest("Missing email.");

        var listIds = await _profilesService.GetProfileListIdsByEmailAsync(email, storeAlias, ct);
        return listIds is null ? NotFound() : Ok(listIds);
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> UpsertAsync(
        [FromBody] ProfileUpsertRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return BadRequest("Missing request body.");
        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return BadRequest("Missing storeAlias.");

        if (string.IsNullOrWhiteSpace(request.Email) &&
            string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            string.IsNullOrWhiteSpace(request.ExternalId))
        {
            return BadRequest("Missing customer identifier (email, phoneNumber, or externalId).");
        }

        var customer = new KlaviyoCustomer
        {
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            ExternalId = request.ExternalId,
            KlaviyoProfileId = request.KlaviyoProfileId
        };

        var attributes = new KlaviyoProfileAttributes
        {
            FullName = request.FullName,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Address = request.Address,
            Address2 = request.Address2,
            ZipCode = request.ZipCode,
            City = request.City,
            Country = request.Country,
            Organisation = request.Organisation,
            CustomProperties = request.CustomProperties
        };

        var update = new KlaviyoProfileUpdate(
            StoreAlias: request.StoreAlias,
            Profile: new KlaviyoProfile
            {
                Customer = customer,
                Attributes = attributes
            },
            ListId: request.ListId);

        await _profilesService.UpsertProfileAsync(update, ct);
        return Ok();
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> SubscribeAsync(
        [FromBody] ProfileConsentRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return BadRequest("Missing request body.");
        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return BadRequest("Missing storeAlias.");
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Missing email.");

        var consent = new KlaviyoProfileConsentChange(
            Channel: request.Channel,
            State: KlaviyoProfileConsentState.Subscribed,
            Source: request.Source,
            TimestampUtc: request.TimestampUtc ?? DateTimeOffset.UtcNow);

        var payload = new KlaviyoProfileConsentRequest(
            StoreAlias: request.StoreAlias,
            Email: request.Email,
            Consents: new List<KlaviyoProfileConsentChange> { consent },
            ListId: request.ListId);

        await _profilesService.SubscribeAsync(payload, ct);
        return Ok();
    }

    [HttpPost("upsert-and-subscribe")]
    public async Task<IActionResult> UpsertAndSubscribeAsync(
        [FromBody] ProfileUpsertAndSubscribeRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return BadRequest("Missing request body.");
        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return BadRequest("Missing storeAlias.");
        if (string.IsNullOrWhiteSpace(request.Email) &&
            string.IsNullOrWhiteSpace(request.PhoneNumber) &&
            string.IsNullOrWhiteSpace(request.ExternalId))
        {
            return BadRequest("Missing customer identifier (email, phoneNumber, or externalId).");
        }
        if (string.IsNullOrWhiteSpace(request.ConsentEmail))
            return BadRequest("Missing consent email.");

        var customer = new KlaviyoCustomer
        {
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            ExternalId = request.ExternalId,
            KlaviyoProfileId = request.KlaviyoProfileId
        };

        var attributes = new KlaviyoProfileAttributes
        {
            FullName = request.FullName,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Address = request.Address,
            Address2 = request.Address2,
            ZipCode = request.ZipCode,
            City = request.City,
            Country = request.Country,
            Organisation = request.Organisation,
            CustomProperties = request.CustomProperties
        };

        var update = new KlaviyoProfileUpdate(
            StoreAlias: request.StoreAlias,
            Profile: new KlaviyoProfile
            {
                Customer = customer,
                Attributes = attributes
            },
            ListId: request.ListId);

        var consent = new KlaviyoProfileConsentRequest(
            StoreAlias: request.StoreAlias,
            Email: request.ConsentEmail,
            Consents: new List<KlaviyoProfileConsentChange>
            {
                new(
                    Channel: request.Channel,
                    State: KlaviyoProfileConsentState.Subscribed,
                    Source: request.Source,
                    TimestampUtc: request.TimestampUtc ?? DateTimeOffset.UtcNow)
            },
            ListId: request.ListId);

        await _profilesService.UpsertAndSubscribeAsync(update, consent, ct);
        return Ok();
    }

    [HttpPost("unsubscribe")]
    public async Task<IActionResult> UnsubscribeAsync(
        [FromBody] ProfileConsentRequest request,
        CancellationToken ct = default)
    {
        if (request is null) return BadRequest("Missing request body.");
        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return BadRequest("Missing storeAlias.");
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("Missing email.");

        var consent = new KlaviyoProfileConsentChange(
            Channel: request.Channel,
            State: KlaviyoProfileConsentState.Unsubscribed,
            Source: request.Source,
            TimestampUtc: request.TimestampUtc ?? DateTimeOffset.UtcNow);

        var payload = new KlaviyoProfileConsentRequest(
            StoreAlias: request.StoreAlias,
            Email: request.Email,
            Consents: new List<KlaviyoProfileConsentChange> { consent },
            ListId: null);

        await _profilesService.UnsubscribeAsync(payload, ct);
        return Ok();
    }
}

public sealed record ProfileUpsertRequest
{
    public string StoreAlias { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? ExternalId { get; init; }
    public string? KlaviyoProfileId { get; init; }
    public string? FullName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Address { get; init; }
    public string? Address2 { get; init; }
    public string? ZipCode { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? Organisation { get; init; }
    public IDictionary<string, object?>? CustomProperties { get; init; }
    public string? ListId { get; init; }
}

public sealed record ProfileConsentRequest
{
    public string StoreAlias { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public KlaviyoProfileConsentChannel Channel { get; init; } = KlaviyoProfileConsentChannel.Email;
    public string? Source { get; init; }
    public DateTimeOffset? TimestampUtc { get; init; }
    public string? ListId { get; init; }
}

public sealed record ProfileUpsertAndSubscribeRequest
{
    public string StoreAlias { get; init; } = string.Empty;
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? ExternalId { get; init; }
    public string? KlaviyoProfileId { get; init; }
    public string? FullName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Address { get; init; }
    public string? Address2 { get; init; }
    public string? ZipCode { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? Organisation { get; init; }
    public IDictionary<string, object?>? CustomProperties { get; init; }
    public string? ListId { get; init; }
    public string ConsentEmail { get; init; } = string.Empty;
    public KlaviyoProfileConsentChannel Channel { get; init; } = KlaviyoProfileConsentChannel.Email;
    public string? Source { get; init; }
    public DateTimeOffset? TimestampUtc { get; init; }
}
