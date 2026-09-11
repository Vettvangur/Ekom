using Ekom.Algolia.Indexing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Web.BackOffice.Controllers;

namespace Ekom.Algolia.Controllers;

public class EkomAlgoliaBackofficeController : UmbracoAuthorizedApiController
{
    private readonly IAlgoliaFullIndexRebuildCoordinator _fullIndexRebuildCoordinator;
    private readonly ILogger<EkomAlgoliaBackofficeController> _logger;

    public EkomAlgoliaBackofficeController(
        IAlgoliaFullIndexRebuildCoordinator fullIndexRebuildCoordinator,
        ILogger<EkomAlgoliaBackofficeController> logger)
    {
        _fullIndexRebuildCoordinator = fullIndexRebuildCoordinator;
        _logger = logger;
    }

    [HttpGet]
    [HttpPost]
    public IActionResult RebuildIndexesAsync()
    {
        if (!_fullIndexRebuildCoordinator.TryStart())
            return Conflict(new { error = "An Algolia full reindex is already running." });

        _logger.LogInformation("Algolia manual reindex requested for all configured stores and content indexes.");

        return Ok(new { message = "Algolia product, category, and content reindex initiated." });
    }

    [HttpGet]
    [HttpPost]
    public IActionResult RebuildStoreIndexesAsync([FromQuery] string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest(new { error = "Store alias is required." });

        if (!_fullIndexRebuildCoordinator.TryStartStore(storeAlias))
            return Conflict(new { error = $"An Algolia reindex is already running for store '{storeAlias}'." });

        _logger.LogInformation("Algolia manual reindex requested for store {StoreAlias}.", storeAlias);

        return Ok(new { message = $"Algolia product and category reindex initiated for store '{storeAlias}'." });
    }
}
