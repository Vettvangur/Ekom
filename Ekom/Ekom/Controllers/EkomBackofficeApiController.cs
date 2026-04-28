using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Ekom.Controllers;

/// <summary>
/// 
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "Async controller actions don't need ConfigureAwait")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style",
    "VSTHRD200:Use \"Async\" suffix for async methods",
    Justification = "Async controller action")]

[Route("ekom/backoffice")]
[CamelCaseJson]
public class EkomBackofficeApiController : ControllerBase
{
    private readonly Configuration _config;
    private readonly IUmbracoService _umbracoService;
    private readonly IMetafieldService _metafieldService;
    private readonly INodeService _nodeService;
    private readonly IMemoryCache _memoryCache;

    public EkomBackofficeApiController(Configuration config, IUmbracoService umbracoService, IMetafieldService metafieldService, INodeService nodeService, IMemoryCache memoryCache)
    {
        _config = config;
        _umbracoService = umbracoService;
        _metafieldService = metafieldService;
        _nodeService = nodeService;
        _memoryCache = memoryCache;
    }


    [HttpGet]
    [Route("GetNonEkomDataTypes")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IEnumerable<object> GetNonEkomDataTypes()
        => _umbracoService.GetNonEkomDataTypes();

    [HttpGet]
    [Route("DataType/{id:guid}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public object GetDataTypeById(Guid id)
        => _umbracoService.GetDataTypeById(id);


    [HttpGet]
    [Route("DataType/{contentTypeAlias}/propertyAlias/{propertyAlias}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public object? GetDataTypeByAlias(
        string contentTypeAlias,
        string propertyAlias)
        => _umbracoService.GetDataTypeByAlias(contentTypeAlias, propertyAlias);

    [HttpGet]
    [Route("Metafields")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IEnumerable<Metafield> GetMetafields()
        => _metafieldService.GetMetafields();

    [HttpGet]
    [Route("Languages")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public IEnumerable<object> GetLanguages()
        => _umbracoService.GetLanguages();

    [HttpGet]
    [Route("Languages/{id:int}")]
    [UmbracoUserAuthorize]
    public IEnumerable<object> GetLanguagesByNode([FromRoute] int id)
    {
        var stores = LoadStores(id);
        var supportedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var store in stores)
        {
            foreach (var culture in store.Cultures)
            {
                if (!string.IsNullOrWhiteSpace(culture.Name))
                {
                    supportedCultures.Add(culture.Name);
                }
            }
        }

        var languages = _umbracoService.GetLanguages();

        if (languages == null)
        {
            return Array.Empty<object>();
        }

        if (supportedCultures.Count == 0)
        {
            return languages.ToList();
        }

        return languages.Where(language => supportedCultures.Contains(language.IsoCode)).ToList();
    }

    [HttpGet]
    [Route("Stores")]
    [UmbracoUserAuthorize]
    public async Task<IEnumerable<IStore>> GetAllStores()
    {
        return await _memoryCache.GetOrCreateAsync("AllStores", async cacheEntry =>
        {
            cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10);
            return API.Store.Instance.GetAllStores();
        });
    }

    private static readonly ConcurrentDictionary<string, Lazy<Task<IEnumerable<IStore>>>> _storeLocks = new();

    [HttpGet]
    [Route("Stores/{id}")]
    [UmbracoUserAuthorize]
    public async Task<IEnumerable<IStore>> GetStores([FromRoute] int id)
    {
        var cacheKey = $"Stores_{id}";

        if (_memoryCache.TryGetValue<IEnumerable<IStore>>(cacheKey, out var cached))
        {
            return cached;
        }

        var lazy = _storeLocks.GetOrAdd(cacheKey, key =>
            new Lazy<Task<IEnumerable<IStore>>>(async () =>
            {
                var data = LoadStores(id);
                _memoryCache.Set(cacheKey, data, TimeSpan.FromSeconds(60));
                _storeLocks.TryRemove(cacheKey, out _);
                return data;
            }));

        try
        {
            return await lazy.Value;
        }
        catch
        {
            // If the factory failed, remove it so future attempts can retry
            _storeLocks.TryRemove(cacheKey, out _);
            throw;
        }
    }

    private IEnumerable<IStore> LoadStores(int id)
    {
        var allStores = API.Store.Instance.GetAllStores();
        var node = _nodeService.NodeById(id, true);
        if (node == null)
            return allStores;

        var ancestors = _nodeService.GetAllCatalogAncestors(node);
        var disabledAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<IStore>();

        foreach (var store in allStores)
        {
            var alias = store.Alias;

            // First check if this store is disabled on the node itself
            var isSelfDisabled = node.Properties.GetValue("disable", alias).IsBoolean();
            if (isSelfDisabled)
            {
                disabledAliases.Add(alias);
                continue;
            }

            // Skip ancestor check if already disabled
            bool isDisabledInAncestors = false;
            foreach (var ancestor in ancestors)
            {
                if (ancestor.Properties.GetValue("disable", alias).IsBoolean())
                {
                    isDisabledInAncestors = true;
                    disabledAliases.Add(alias);
                    break;
                }
            }

            if (!isDisabledInAncestors)
            {
                result.Add(store);
            }
        }

        return result;
    }

    /// <summary>
    /// Repopulates all Ekom cache
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("Cache")]
    [UmbracoUserAuthorize]
    public bool PopulateCache()
    {
        API.Store.Instance.RefreshCache();

        return true;
    }

    /// <summary>
    /// Get Config
    /// </summary>
    [HttpGet]
    [Route("Config")]
    [UmbracoUserAuthorize]
    public Configuration GetConfig()
    {
        return _config;
    }

    /// <summary>
    /// Get Stock By Store
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("Stock/{id:Guid}/StoreAlias/{storeAlias}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public decimal GetStockByStore(Guid id, string storeAlias)
    {
        return API.Stock.Instance.GetStock(id, storeAlias);
    }

    /// <summary>
    /// Get Stock 
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    [Route("Stock/{id:Guid}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public decimal GetStock(Guid id)
    {
        return API.Stock.Instance.GetStock(id);
    }

    /// <summary>
    /// Increment stock count of item. 
    /// If PerStoreStock is configured, gets store from cache and updates relevant item.
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// </summary>
    [HttpPatch]
    [Route("stock/{id:Guid}/value/{stock}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> IncrementStock(Guid id, decimal stock)
    {
        try
        {
            await API.Stock.Instance.IncrementStockAsync(id, stock);

            return Ok();
        }
        catch (Exception ex)
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }


    /// <summary>
    /// Increment stock count of store item. 
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// </summary>
    [HttpPatch]
    [Route("stock/{id:Guid}/StoreAlias/{storeAlias}/value/{stock}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> IncrementStock(Guid id, string storeAlias, decimal stock)
    {
        try
        {
            await API.Stock.Instance.IncrementStockAsync(id, storeAlias, stock);

            return Ok();
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Sets stock count of item. 
    /// If PerStoreStock is configured, gets store from cache and updates relevant item.
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// </summary>
    [HttpPut]
    [Route("stock/{id:Guid}/value/{stock}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> SetStock(Guid id, decimal stock)
    {
        try
        {
            await API.Stock.Instance.SetStockAsync(id, stock);

            return Ok();
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Sets stock count of store item. 
    /// If no stock entry exists, creates a new one, then attempts to update.
    /// </summary>
    [HttpPut]
    [Route("stock/{id:Guid}/StoreAlias/{storeAlias}/value/{stock}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> SetStock(Guid id, string storeAlias, decimal stock)
    {
        try
        {
            await API.Stock.Instance.SetStockAsync(id, storeAlias, stock);

            return Ok();
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Insert Coupon
    /// </summary>
    [HttpPost]
    [Route("coupon/{couponCode}/NumberAvailable/{numberAvailable}/discountId/{id:Guid}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> InsertCoupon(string couponCode, int numberAvailable, Guid id, CancellationToken ct = default)
    {
        try
        {
            await API.Order.Instance.InsertCouponCodeAsync(couponCode, numberAvailable, id, ct: ct);
            return Ok();
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            if (ex.Message == "Duplicate coupon")
            {
                return Conflict("A coupon with this code already exists.");
            }

            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Generate Coupons
    /// </summary>
    [HttpPost]
    [Route("coupon/generate/discountId/{id:Guid}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> GenerateCoupons(Guid id, [FromBody] CouponGenerationRequest request, CancellationToken ct = default)
    {
        try
        {
            CouponGenerationResult result = await API.Order.Instance.GenerateCouponCodesAsync(id, request, ct);
            return Ok(result);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Export Coupons
    /// </summary>
    [HttpGet]
    [Route("coupon/export/discountId/{id:Guid}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ExportCoupons(Guid id, CancellationToken ct = default)
    {
        try
        {
            List<CouponData> coupons = await API.Order.Instance.GetCouponsForDiscountAsync(id, ct);
            var csv = CreateCouponCsv(coupons);
            var fileName = $"coupons-{id:N}.csv";

            return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Remove Coupon
    /// </summary>
    [HttpDelete]
    [Route("coupon/{couponCode}/discountId/{id:Guid}")]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> RemoveCoupon(string couponCode, Guid id)
    {
        try
        {
            await API.Order.Instance.RemoveCouponCodeAsync(couponCode, id);

            return Ok();
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    /// <summary>
    /// Get Coupons for Discount
    /// </summary>
    [HttpGet]
    [Route("coupon/discountId/{id:Guid}")]
    [UmbracoUserAuthorize]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetCouponsForDiscount(Guid id, string query = "", int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            (List<CouponData> Data, int TotalPages) items = await API.Order.Instance.GetCouponsForDiscountAsync(id, query, page, pageSize, ct);
            return Ok(items);
        }
        catch (Exception ex)
        {
            var result = ExceptionHandler.Handle(ex);
            return result ?? StatusCode(500, "An unexpected error occurred.");
        }
    }

    private static string CreateCouponCsv(IEnumerable<CouponData> coupons)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CouponCode,NumberAvailable,Date");

        foreach (CouponData coupon in coupons)
        {
            builder
                .Append(EscapeCsv(coupon.CouponCode))
                .Append(',')
                .Append(coupon.NumberAvailable)
                .Append(',')
                .Append(EscapeCsv(coupon.Date.ToString("O")))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
