using Ekom.Algolia.Indexing;
using Ekom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;

namespace Ekom.Algolia.Controllers;

[Authorize(AuthenticationSchemes = Constants.Security.BackOfficeAuthenticationType)]
[Route("umbraco/backoffice/api/EkomAlgoliaBackoffice")]
public class EkomAlgoliaBackofficeController : ControllerBase
{
    private readonly IAlgoliaFullIndexRebuildCoordinator _fullIndexRebuildCoordinator;
    private readonly IAlgoliaProductIndexService _algoliaProductIndexService;
    private readonly IAlgoliaCategoryIndexService _algoliaCategoryIndexService;
    private readonly ISecurityService _securityService;
    private readonly ILogger<EkomAlgoliaBackofficeController> _logger;

    public EkomAlgoliaBackofficeController(
        IAlgoliaFullIndexRebuildCoordinator fullIndexRebuildCoordinator,
        IAlgoliaProductIndexService algoliaProductIndexService,
        IAlgoliaCategoryIndexService algoliaCategoryIndexService,
        ISecurityService securityService,
        ILogger<EkomAlgoliaBackofficeController> logger)
    {
        _fullIndexRebuildCoordinator = fullIndexRebuildCoordinator;
        _algoliaProductIndexService = algoliaProductIndexService;
        _algoliaCategoryIndexService = algoliaCategoryIndexService;
        _securityService = securityService;
        _logger = logger;
    }

    [HttpGet("RebuildIndexes")]
    [HttpPost("RebuildIndexes")]
    public IActionResult RebuildIndexesAsync()
    {
        if (!_securityService.IsCurrentUserAdmin())
            return Forbid();

        if (!_fullIndexRebuildCoordinator.TryStart())
            return Conflict(new { error = "An Algolia full reindex is already running." });

        _logger.LogInformation("Algolia manual reindex requested for all configured stores and content indexes.");

        return Ok(new { message = "Algolia product, category, and content reindex initiated." });
    }

    [HttpGet("RebuildStoreIndexes")]
    [HttpPost("RebuildStoreIndexes")]
    public async Task<IActionResult> RebuildStoreIndexesAsync([FromQuery] string storeAlias, CancellationToken ct = default)
    {
        if (!_securityService.IsCurrentUserAdmin())
            return Forbid();

        if (string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest(new { error = "Store alias is required." });

        await _algoliaProductIndexService.RebuildStoreAsync(storeAlias, ct).ConfigureAwait(false);
        await _algoliaCategoryIndexService.RebuildStoreAsync(storeAlias, ct).ConfigureAwait(false);

        _logger.LogInformation("Algolia manual reindex requested for store {StoreAlias}.", storeAlias);

        return Ok(new { message = $"Algolia product and category reindex initiated for store '{storeAlias}'." });
    }
}
