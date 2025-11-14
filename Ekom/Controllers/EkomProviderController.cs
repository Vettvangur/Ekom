using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers;

/// <summary>
/// Provider catalog
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "Async controller actions don't need ConfigureAwait")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style",
    "VSTHRD200:Use \"Async\" suffix for async methods",
    Justification = "Async controller action")]
[Route("ekom/provider")]
[ServiceFilter(typeof(ApiExceptionFilter))]
public class EkomProviderController : ControllerBase
{
    private readonly ControllerRequestHelper _reqHelper;

    /// <summary>
    /// ctor
    /// </summary>
    public EkomProviderController(ControllerRequestHelper reqHelper)
    {
        _reqHelper = reqHelper;
    }

    /// <summary>
    /// Get Payment Providers
    /// </summary>
    /// <param name="countryCode"></param>
    /// <param name="orderAmount"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("paymentsproviders/{storeAlias?}")]
    public IActionResult GetPaymentProviders([FromQuery] string countryCode, [FromQuery] decimal orderAmount, string? storeAlias = null)
    {
        _reqHelper.SetEkmRequest(storeAlias: storeAlias);

        IStore? store = API.Store.Instance.GetStore(storeAlias);

        if (store == null)
        {
            return NotFound($"Store {storeAlias} not found");
        }

        return Ok(API.Providers.Instance.GetPaymentProviders(store.Alias, countryCode, orderAmount));

    }

    /// <summary>
    /// Get Payment Provider
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("paymentsprovider/{id:Guid}")]
    public IActionResult GetPaymentProvider([FromRoute] Guid id)
    {
        IStore? store = API.Store.Instance.GetStore();

        if (store == null)
        {
            return NotFound($"Store not found");
        }

        return Ok(API.Providers.Instance.GetPaymentProvider(id, store));
    }

    /// <summary>
    /// Get Shipping Providers
    /// </summary>
    /// <param name="countryCode"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("shippingproviders/{storeAlias?}")]
    public async Task<IActionResult> GetShippingProviders([FromQuery] string countryCode, string? storeAlias = null)
    {
        _reqHelper.SetEkmRequest(storeAlias: storeAlias);

        IStore? store = API.Store.Instance.GetStore(storeAlias);

        if (store == null)
        {
            return NotFound($"Store {storeAlias} not found");
        }

        var order = await Ekom.API.Order.Instance.GetOrderAsync();

        var orderAmount = order != null ? order.ChargedAmount.Value - (order.ShippingProvider != null ? order.ShippingProvider.Price.Value : 0) : 0;

        return Ok(API.Providers.Instance.GetShippingProviders(store.Alias, countryCode, orderAmount));

    }

    /// <summary>
    /// Get Shipping Provider
    /// </summary>
    /// <param name="id"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("shippingprovider/{id:Guid}")]
    public IActionResult GetShippingProvider([FromRoute] Guid id)
    {
        IStore? store = API.Store.Instance.GetStore();

        if (store == null)
        {
            return NotFound($"Store not found");
        }

        return Ok(API.Providers.Instance.GetShippingProvider(id, store));
    }

    /// <summary>
    /// Get All Zones
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("zones")]
    public IActionResult GetAllZones()
    {
        return Ok(API.Providers.Instance.GetAllZones());
    }

}
