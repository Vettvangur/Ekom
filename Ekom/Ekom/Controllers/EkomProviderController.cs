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
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpGet]
    [Route("paymentsproviders/{storeAlias?}")]
    public async Task<IActionResult> GetPaymentProvidersAsync([FromQuery] string countryCode, [FromQuery] decimal orderAmount, string? storeAlias = null, CancellationToken ct = default)
    {
        _reqHelper.SetEkmRequest(storeAlias: storeAlias);

        IStore? store = API.Store.Instance.GetStore(storeAlias);

        if (store == null)
        {
            return NotFound($"Store {storeAlias} not found");
        }

        var providers = await API.Providers.Instance.GetPaymentProvidersAsync(store.Alias, countryCode, orderAmount, ct);

        return Ok(providers);
    }

    /// <summary>
    /// Get Payment Provider
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpGet]
    [Route("paymentsprovider/{id:Guid}")]
    public async Task<IActionResult> GetPaymentProviderAsync([FromRoute] Guid id, CancellationToken ct = default)
    {
        IStore? store = API.Store.Instance.GetStore();

        if (store == null)
        {
            return NotFound($"Store not found");
        }

        var provider = await API.Providers.Instance.GetPaymentProviderAsync(id, store, ct);

        return Ok(provider);
    }

    /// <summary>
    /// Get Shipping Providers
    /// </summary>
    /// <param name="countryCode"></param>
    /// <param name="storeAlias"></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpGet]
    [Route("shippingproviders/{storeAlias?}")]
    public async Task<IActionResult> GetShippingProvidersAsync([FromQuery] string countryCode, string? storeAlias = null, CancellationToken ct = default)
    {
        _reqHelper.SetEkmRequest(storeAlias: storeAlias);

        IStore? store = API.Store.Instance.GetStore(storeAlias);

        if (store == null)
        {
            return NotFound($"Store {storeAlias} not found");
        }

        var order = await API.Order.Instance.GetOrderAsync(ct);

        var orderAmount = order != null ? order.ChargedAmount.Value - (order.ShippingProvider != null ? order.ShippingProvider.Price.Value : 0) : 0;

        var providers = await API.Providers.Instance.GetShippingProvidersAsync(store.Alias, countryCode, orderAmount, ct);

        return Ok(providers);
    }

    /// <summary>
    /// Get Shipping Provider
    /// </summary>
    /// <param name="id"></param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpGet]
    [Route("shippingprovider/{id:Guid}")]
    public async Task<IActionResult> GetShippingProviderAsync([FromRoute] Guid id, CancellationToken ct = default)
    {
        IStore? store = API.Store.Instance.GetStore();

        if (store == null)
        {
            return NotFound($"Store not found");
        }

        var provider = await API.Providers.Instance.GetShippingProviderAsync(id, store, ct);

        return Ok(provider);
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
