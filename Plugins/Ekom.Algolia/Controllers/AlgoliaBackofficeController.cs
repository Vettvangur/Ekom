using Ekom.Algolia.Indexing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.BackOffice.Controllers;

namespace Ekom.Algolia.Controllers;

public class EkomAlgoliaBackofficeController : UmbracoAuthorizedApiController
{
    private readonly IAlgoliaProductIndexService _algoliaProductIndexService;
    private readonly ILogger<EkomAlgoliaBackofficeController> _logger;

    public EkomAlgoliaBackofficeController(
        IAlgoliaProductIndexService algoliaProductIndexService,
        ILogger<EkomAlgoliaBackofficeController> logger)
    {
        _algoliaProductIndexService = algoliaProductIndexService;
        _logger = logger;
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> RebuildIndexesAsync(CancellationToken ct = default)
    {
        await _algoliaProductIndexService.RebuildAllAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Algolia manual reindex requested for all configured stores.");

        return Ok(new { message = "Algolia product reindex initiated for all configured stores." });
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> RebuildStoreIndexesAsync([FromQuery] string storeAlias, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest(new { error = "Store alias is required." });

        await _algoliaProductIndexService.RebuildStoreAsync(storeAlias, ct).ConfigureAwait(false);

        _logger.LogInformation("Algolia manual reindex requested for store {StoreAlias}.", storeAlias);

        return Ok(new { message = $"Algolia product reindex initiated for store '{storeAlias}'." });
    }
}
