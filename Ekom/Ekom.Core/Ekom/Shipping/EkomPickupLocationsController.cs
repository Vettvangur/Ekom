using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ekom.Shipping;

[ApiController]
[Route("ekom/shipping/providers")]
[ServiceFilter(typeof(global::Ekom.ApiExceptionFilter))]
public sealed class EkomPickupLocationsController : ControllerBase
{
    private readonly IEkomPickupLocationQueryService _queryService;
    private readonly ILogger<EkomPickupLocationsController> _logger;

    public EkomPickupLocationsController(
        IEkomPickupLocationQueryService queryService,
        ILogger<EkomPickupLocationsController> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    [HttpGet("{providerKey:guid}/pickup-locations")]
    [ProducesResponseType(typeof(IReadOnlyList<PickupLocation>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PickupLocation>>> GetAsync(
        [FromRoute] Guid providerKey,
        [FromQuery] string? storeAlias = null,
        [FromQuery] string? countryCode = "IS",
        [FromQuery] string? postalCode = null,
        CancellationToken cancellationToken = default)
    {
        countryCode = countryCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (countryCode.Length != 2 || countryCode.Any(x => x is < 'A' or > 'Z'))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid country code",
                "countryCode must contain two letters."));
        }

        storeAlias = string.IsNullOrWhiteSpace(storeAlias) ? null : storeAlias.Trim();
        postalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
        var invalidIcelandicPostcode = string.Equals(countryCode, "IS", StringComparison.Ordinal) &&
            postalCode is not null &&
            (!int.TryParse(postalCode, NumberStyles.None, CultureInfo.InvariantCulture, out var postcode) ||
             postcode is < 100 or > 999);
        if (storeAlias?.Length > 100 || postalCode?.Length > 20 || invalidIcelandicPostcode)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid query",
                "storeAlias or postalCode is invalid."));
        }

        try
        {
            var locations = await _queryService.GetAsync(
                providerKey,
                storeAlias,
                countryCode,
                postalCode,
                cancellationToken).ConfigureAwait(false);
            if (locations is null)
            {
                return NotFound(CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Shipping provider not found",
                    "The store or shipping provider was not found."));
            }

            return Ok(locations);
        }
        catch (ShippingProviderException exception)
        {
            _logger.LogWarning(exception, "Pickup location lookup failed for provider {ProviderKey}", providerKey);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                CreateProblem(
                    StatusCodes.Status502BadGateway,
                    "Carrier unavailable",
                    "Pickup locations could not be loaded from the carrier."));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Pickup location transport failed for provider {ProviderKey}", providerKey);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                CreateProblem(
                    StatusCodes.Status502BadGateway,
                    "Carrier unavailable",
                    "Pickup locations could not be loaded from the carrier."));
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Pickup location lookup timed out for provider {ProviderKey}", providerKey);
            return StatusCode(
                StatusCodes.Status504GatewayTimeout,
                CreateProblem(
                    StatusCodes.Status504GatewayTimeout,
                    "Carrier timeout",
                    "The carrier did not return pickup locations in time."));
        }
    }

    private static ProblemDetails CreateProblem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
    };
}
