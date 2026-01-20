using Ekom.Klaviyo.Mappers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Web.BackOffice.Controllers;
using Umbraco.Cms.Web.Common.Attributes;

namespace Ekom.Klaviyo.Controllers;

[PluginController("Ekom")]
internal class KlaviyoBackofficeController : UmbracoAuthorizedApiController
{
    private readonly IKlaviyoClient _klaviyoClient;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoBackofficeController> _logger;

    public KlaviyoBackofficeController(
        IKlaviyoClient klaviyoClient, 
        IOptions<KlaviyoOptions> opt, 
        ILogger<KlaviyoBackofficeController> logger)
    {
        _klaviyoClient = klaviyoClient;
        _opt = opt.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> BuildProducts(CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.ProductEvents.Enabled) { return BadRequest("Klaviyo integration is disabled."); }
        
        _logger.LogInformation("Building products for Klaviyo integration.");

        foreach (var storeAlias in _opt.Stores)
        {
            _logger.LogInformation("Building products for store {StoreAlias}.", storeAlias);

            var productsResponse = Ekom.API.Catalog.Instance.GetAllProducts(storeAlias);

            var products = productsResponse.Products.ToKlaviyoCatalogItems(true, _opt.Host).ToList();

            await _klaviyoClient.BulkCreateCatalogItemsAsync(products, ct);

            _logger.LogInformation("Completed building products for store {StoreAlias}. Products: {ProductsCount}", storeAlias, products.Count);
        }

        _logger.LogInformation("Completed building products for Klaviyo integration.");

        return Ok();
    }

}
