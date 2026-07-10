using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Umb.VariantApp.Models;
using Ekom.Umb.VariantApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Umb.VariantApp.Controllers;

[Route("ekom/backoffice/Variants")]
[CamelCaseJson]
public sealed class VariantAppController : ControllerBase
{
    private readonly IVariantAppService _variantAppService;

    public VariantAppController(IVariantAppService variantAppService)
    {
        _variantAppService = variantAppService;
    }

    [HttpGet]
    [Route("{productId}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public VariantManagerProduct GetProductVariants([FromRoute] string productId)
        => _variantAppService.GetProductVariants(productId);

    [HttpGet]
    [Route("{productId}/Count")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public VariantManagerCount GetVariantCount([FromRoute] string productId)
        => _variantAppService.GetVariantCount(productId);

    [HttpPost]
    [Route("Groups")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public VariantManagerGroup CreateVariantGroup([FromBody] VariantManagerGroupRequest request)
        => _variantAppService.CreateVariantGroup(request);

    [HttpPost]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public Task<VariantManagerVariant> CreateVariant([FromBody] VariantManagerVariantRequest request)
        => _variantAppService.CreateVariantAsync(request);

    [HttpPost]
    [Route("Save")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public Task<VariantManagerProduct> SaveProductVariants([FromBody] VariantManagerSaveRequest request)
        => _variantAppService.SaveProductVariantsAsync(request);

    [HttpPost]
    [Route("Groups/Save")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public VariantManagerGroup SaveVariantGroup([FromBody] VariantManagerGroupSaveRequest request)
        => _variantAppService.SaveVariantGroup(request);

    [HttpPost]
    [Route("Items/Save")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public Task<VariantManagerVariant> SaveVariant([FromBody] VariantManagerVariantSaveRequest request)
        => _variantAppService.SaveVariantAsync(request);

    [HttpGet]
    [Route("Media/Thumbnail")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult GetMediaThumbnail([FromQuery] string mediaId, [FromQuery] int width = 38, [FromQuery] int height = 38)
    {
        var url = _variantAppService.GetMediaThumbnailPath(mediaId, width, height);
        return string.IsNullOrWhiteSpace(url) ? NotFound() : Redirect(url);
    }

    [HttpDelete]
    [Route("{nodeId}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public bool DeleteVariantNode([FromRoute] string nodeId)
        => _variantAppService.DeleteVariantNode(nodeId);
}
