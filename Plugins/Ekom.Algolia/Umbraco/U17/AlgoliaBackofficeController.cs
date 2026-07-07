using Ekom.Algolia.Indexing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Api.Management.Controllers;

namespace Ekom.Algolia.Controllers;

[Route("umbraco/backoffice/api/Ekom/AlgoliaBackoffice")]
public class EkomAlgoliaBackofficeController : ManagementApiControllerBase
{
    private readonly IAlgoliaProductIndexService _algoliaProductIndexService;
    private readonly IAlgoliaCategoryIndexService _algoliaCategoryIndexService;
    private readonly IAlgoliaContentIndexService _algoliaContentIndexService;
    private readonly ILogger<EkomAlgoliaBackofficeController> _logger;

    public EkomAlgoliaBackofficeController(
        IAlgoliaProductIndexService algoliaProductIndexService,
        IAlgoliaCategoryIndexService algoliaCategoryIndexService,
        IAlgoliaContentIndexService algoliaContentIndexService,
        ILogger<EkomAlgoliaBackofficeController> logger)
    {
        _algoliaProductIndexService = algoliaProductIndexService;
        _algoliaCategoryIndexService = algoliaCategoryIndexService;
        _algoliaContentIndexService = algoliaContentIndexService;
        _logger = logger;
    }

    [HttpGet("RebuildIndexes")]
    [HttpPost("RebuildIndexes")]
    public async Task<IActionResult> RebuildIndexesAsync(CancellationToken ct = default)
    {
        await _algoliaProductIndexService.RebuildAllAsync(ct).ConfigureAwait(false);
        await _algoliaCategoryIndexService.RebuildAllAsync(ct).ConfigureAwait(false);
        await _algoliaContentIndexService.RebuildAsync(ct: ct).ConfigureAwait(false);

        _logger.LogInformation("Algolia manual reindex requested for all configured stores and content indexes.");

        return Ok(new { message = "Algolia product, category, and content reindex initiated." });
    }

    [HttpGet("RebuildStoreIndexes")]
    [HttpPost("RebuildStoreIndexes")]
    public async Task<IActionResult> RebuildStoreIndexesAsync([FromQuery] string storeAlias, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest(new { error = "Store alias is required." });

        await _algoliaProductIndexService.RebuildStoreAsync(storeAlias, ct).ConfigureAwait(false);
        await _algoliaCategoryIndexService.RebuildStoreAsync(storeAlias, ct).ConfigureAwait(false);

        _logger.LogInformation("Algolia manual reindex requested for store {StoreAlias}.", storeAlias);

        return Ok(new { message = $"Algolia product and category reindex initiated for store '{storeAlias}'." });
    }
}
