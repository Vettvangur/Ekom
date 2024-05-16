using Ekom.Exceptions;
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
    [Route("paymentsproviders/{storeAlias}")]
    public IEnumerable<IPaymentProvider> GetPaymentProviders(string countryCode, decimal orderAmount, string? storeAlias = null)
    {
        try
        {
            return API.Providers.Instance.GetPaymentProviders(storeAlias, countryCode, orderAmount);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Payment Provider
    /// </summary>
    /// <param name="id"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("paymentsprovider/{id:Guid}")]
    public IPaymentProvider GetPaymentProvider([FromRoute]Guid id,  string? storeAlias = null)
    {
        try
        {
            IStore? store = !string.IsNullOrEmpty(storeAlias) ? API.Store.Instance.GetStore(storeAlias) : null;

            return API.Providers.Instance.GetPaymentProvider(id, store);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Shipping Providers
    /// </summary>
    /// <param name="countryCode"></param>
    /// <param name="orderAmount"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("shippingproviders/{storeAlias}")]
    public IEnumerable<IShippingProvider> GetShippingProviders(string countryCode, decimal orderAmount, string? storeAlias = null)
    {
        try
        {
            return API.Providers.Instance.GetShippingProviders(storeAlias, countryCode, orderAmount);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Shipping Provider
    /// </summary>
    /// <param name="id"></param>
    /// <param name="storeAlias"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("shippingprovider/{id:Guid}")]
    public IShippingProvider GetShippingProvider([FromRoute] Guid id, string? storeAlias = null)
    {
        try
        {
            IStore? store = !string.IsNullOrEmpty(storeAlias) ? API.Store.Instance.GetStore(storeAlias) : null;

            return API.Providers.Instance.GetShippingProvider(id, store);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get All Zones
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("zones")]
    public IEnumerable<IZone> GetAllZones()
    {
        try
        {
            return API.Providers.Instance.GetAllZones();
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

}
