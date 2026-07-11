using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Umb.CatalogCollection.Models;
using Ekom.Umb.CatalogCollection.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Umb.CatalogCollection.Controllers;

[Route("ekom/backoffice/CatalogCollection")]
[CamelCaseJson]
public sealed class CatalogCollectionController : ControllerBase
{
    private readonly ICatalogCollectionService _catalogCollectionService;

    public CatalogCollectionController(ICatalogCollectionService catalogCollectionService)
    {
        _catalogCollectionService = catalogCollectionService;
    }

    [HttpGet]
    [Route("{nodeId}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public CatalogCollectionResponse GetCollection([FromRoute] string nodeId, [FromQuery] CatalogCollectionRequest request)
        => _catalogCollectionService.GetCollection(nodeId, request);
}
