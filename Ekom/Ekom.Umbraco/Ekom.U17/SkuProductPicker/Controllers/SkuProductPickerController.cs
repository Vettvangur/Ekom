using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Umb.SkuProductPicker.Models;
using Ekom.Umb.SkuProductPicker.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Umb.SkuProductPicker.Controllers;

[Route("ekom/backoffice/SkuProductPicker")]
[CamelCaseJson]
public sealed class SkuProductPickerController : ControllerBase
{
    private readonly ISkuProductPickerService _skuProductPickerService;

    public SkuProductPickerController(ISkuProductPickerService skuProductPickerService)
    {
        _skuProductPickerService = skuProductPickerService;
    }

    [HttpPost("keys")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IReadOnlyList<SkuProductPickerItem> ResolveKeys([FromBody] SkuProductPickerKeysRequest request)
        => _skuProductPickerService.ResolveKeys(request.Keys);

    [HttpPost("skus")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IReadOnlyList<SkuProductPickerItem> ResolveSkus([FromBody] SkuProductPickerSkusRequest request)
        => _skuProductPickerService.ResolveSkus(request.Skus);
}
