using Ekom.Exceptions;
using Ekom.Klaviyo.Enrichers.ProductFeedEnricher;
using Ekom.Klaviyo.Events;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Catalog;
using Ekom.Models;
using EkomStoreApi = Ekom.API.Store;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Klaviyo.Controllers;

[ApiController]
[Route("ekom/klaviyo/product")]
internal class KlaviyoProductController : ControllerBase
{
    private readonly KlaviyoOptions _opt;
    private readonly IMemoryCache _cache;
    private readonly KlaviyoProductFeedEnrichmentPipeline _pipeline;
    private readonly EkomStoreApi _storeApi;

    public KlaviyoProductController(
        IOptions<KlaviyoOptions> opt,
        IMemoryCache cache,
        KlaviyoProductFeedEnrichmentPipeline pipeline,
        EkomStoreApi storeApi)
    {
        _opt = opt.Value;
        _cache = cache;
        _pipeline = pipeline;
        _storeApi = storeApi;
    }

    [HttpGet("feed")]
    [Produces("application/json")]
    public async Task<IActionResult> GetProductFeedAsync(
        [FromQuery] string? storeAlias = null,
        [FromQuery] string? culture = null,
        CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.Catalog.Enabled || _opt.Catalog.SyncMode != KlaviyoCatalogSyncMode.FeedPull)
            return BadRequest("Klaviyo integration is disabled.");

        if (!IsAuthorized(Request, _opt.Catalog.Username ?? "", _opt.Catalog.Password ?? ""))
        {
            return Unauthorized();
        }

        // Default store if not provided
        storeAlias ??= _opt.Stores?.FirstOrDefault()?.Alias;

        if (string.IsNullOrWhiteSpace(storeAlias))
            return BadRequest("Missing storeAlias and no default store is configured.");

        IStore? store;
        try
        {
            store = _storeApi.GetStore(storeAlias);
        }
        catch (StoreNotFoundException)
        {
            return BadRequest($"Unknown storeAlias '{storeAlias}'.");
        }

        if (store is null)
            return BadRequest($"Unknown storeAlias '{storeAlias}'.");

        storeAlias = store.Alias;

        var resolvedCulture = ResolveCulture(store, culture);
        if (resolvedCulture is null)
            return BadRequest($"Missing culture and store '{storeAlias}' has no default culture configured.");

        if (!IsSupportedCulture(store, resolvedCulture))
            return BadRequest($"Culture '{resolvedCulture}' is not supported by store '{storeAlias}'.");

        var cacheKey = $"klaviyo:feed:v3:{storeAlias}:{resolvedCulture}";

        var json = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);
            entry.Priority = CacheItemPriority.High;

            using var context = ApplyEkomRequestContext(store, resolvedCulture);
            try
            {
                var eventArgs = new KlaviyoProductFeedProductsEventArgs
                {
                    StoreAlias = storeAlias,
                    Culture = resolvedCulture,
                    Store = store
                };

                await KlaviyoProductFeedEvents.InvokeProductFeedProductsLoadingAsync(eventArgs, ct);

                var products = eventArgs.Handled
                    ? eventArgs.Products
                    : (await API.Catalog.Instance.GetAllProductsAsync(storeAlias, ct: ct))?.Products;

                var feed = new List<KlaviyoProductFeedItem>();

                if (products is not null)
                {
                    foreach (var product in products.Where(HasProductImage))
                    {
                        var item = product.ToKlaviyoProductFeedItem(_opt, culture: resolvedCulture);

                        await _pipeline.ApplyAsync(
                            item,
                            new KlaviyoProductFeedEnrichmentContext
                            {
                                StoreAlias = storeAlias,
                                Culture = resolvedCulture,
                                Product = product,
                                FeedItem = item,
                                Options = _opt
                            },
                            ct);

                        if (!HasProductLink(item))
                            continue;

                        feed.Add(item);
                    }
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                return JsonSerializer.Serialize(feed, jsonOptions);
            }
            finally
            {
                context.Restore();
            }
        });

        Response.Headers.CacheControl = "private, max-age=3600";

        if (!string.IsNullOrEmpty(_opt.Catalog.Username) || !string.IsNullOrEmpty(_opt.Catalog.Password))
        {
            Response.Headers.Vary = "Authorization";
        }
          
        return Content(json ?? "", "application/json");
    }

    private string? ResolveCulture(IStore store, string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture) &&
            Request.Query.TryGetValue("Culture", out var cultureValues) &&
            !string.IsNullOrWhiteSpace(cultureValues.FirstOrDefault()))
        {
            culture = cultureValues.FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(culture))
            return culture.Trim();

        return NullIfWhiteSpace(store.Culture?.Name)
            ?? store.Cultures
                .Select(c => c.Name)
                .FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
    }

    private static bool IsSupportedCulture(IStore store, string culture)
    {
        return store.Cultures.Any(c => c.Name.Equals(culture, StringComparison.OrdinalIgnoreCase))
            || store.Culture?.Name.Equals(culture, StringComparison.OrdinalIgnoreCase) == true;
    }

    private RequestContextScope ApplyEkomRequestContext(IStore store, string culture)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var newCulture = CultureInfo.GetCultureInfo(culture);

        CultureInfo.CurrentCulture = newCulture;
        CultureInfo.CurrentUICulture = newCulture;

        var previousRequestCultureFeature = HttpContext.Features.Get<IRequestCultureFeature>();
        HttpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(
                new RequestCulture(newCulture),
                provider: null));

        var previousEkomRequest = HttpContext.Items.TryGetValue(Configuration.EkmRequestKey, out var currentEkomRequest)
            ? currentEkomRequest
            : null;
        var previousShortEkomRequest = HttpContext.Items.TryGetValue("ekmRequest", out var currentShortEkomRequest)
            ? currentShortEkomRequest
            : null;
        var hadEkomRequest = HttpContext.Items.ContainsKey(Configuration.EkmRequestKey);
        var hadShortEkomRequest = HttpContext.Items.ContainsKey("ekmRequest");

        var ekmRequest = ResolveContentRequest(previousEkomRequest);
        ekmRequest.Store = store;

        var lazyRequest = new Lazy<ContentRequest>(() => ekmRequest);
        HttpContext.Items[Configuration.EkmRequestKey] = lazyRequest;
        HttpContext.Items["ekmRequest"] = lazyRequest;

        return new RequestContextScope(
            HttpContext,
            previousCulture,
            previousUiCulture,
            previousRequestCultureFeature,
            previousEkomRequest,
            previousShortEkomRequest,
            hadEkomRequest,
            hadShortEkomRequest);
    }

    private static ContentRequest ResolveContentRequest(object? value)
    {
        return value switch
        {
            Lazy<ContentRequest> lazyContentRequest => lazyContentRequest.Value,
            Lazy<object> lazyObject when lazyObject.Value is ContentRequest contentRequest => contentRequest,
            ContentRequest contentRequest => contentRequest,
            _ => new ContentRequest()
        };
    }

    private sealed class RequestContextScope : IDisposable
    {
        private readonly HttpContext _httpContext;
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;
        private readonly IRequestCultureFeature? _previousRequestCultureFeature;
        private readonly object? _previousEkomRequest;
        private readonly object? _previousShortEkomRequest;
        private readonly bool _hadEkomRequest;
        private readonly bool _hadShortEkomRequest;
        private bool _restored;

        public RequestContextScope(
            HttpContext httpContext,
            CultureInfo previousCulture,
            CultureInfo previousUiCulture,
            IRequestCultureFeature? previousRequestCultureFeature,
            object? previousEkomRequest,
            object? previousShortEkomRequest,
            bool hadEkomRequest,
            bool hadShortEkomRequest)
        {
            _httpContext = httpContext;
            _previousCulture = previousCulture;
            _previousUiCulture = previousUiCulture;
            _previousRequestCultureFeature = previousRequestCultureFeature;
            _previousEkomRequest = previousEkomRequest;
            _previousShortEkomRequest = previousShortEkomRequest;
            _hadEkomRequest = hadEkomRequest;
            _hadShortEkomRequest = hadShortEkomRequest;
        }

        public void Restore()
        {
            if (_restored)
                return;

            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;

            _httpContext.Features.Set(_previousRequestCultureFeature);

            if (_hadEkomRequest)
            {
                _httpContext.Items[Configuration.EkmRequestKey] = _previousEkomRequest;
            }
            else
            {
                _httpContext.Items.Remove(Configuration.EkmRequestKey);
            }

            if (_hadShortEkomRequest)
            {
                _httpContext.Items["ekmRequest"] = _previousShortEkomRequest;
            }
            else
            {
                _httpContext.Items.Remove("ekmRequest");
            }

            _restored = true;
        }

        public void Dispose()
            => Restore();
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool HasProductImage(IProduct product)
    {
        return !string.IsNullOrWhiteSpace(product.Images?.FirstOrDefault()?.Url);
    }

    private static bool HasProductLink(KlaviyoProductFeedItem item)
    {
        return !string.IsNullOrWhiteSpace(item.Link);
    }

    private bool IsAuthorized(HttpRequest request, string expectedUser, string expectedPassword)
    {

        if (string.IsNullOrEmpty(expectedUser) && string.IsNullOrEmpty(expectedPassword)) return true;

        if (!request.Headers.TryGetValue("Authorization", out var header))
            return false;

        var value = header.ToString();
        if (!value.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        var encoded = value["Basic ".Length..].Trim();

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch
        {
            return false;
        }

        var parts = decoded.Split(':', 2);
        if (parts.Length != 2)
            return false;

        return parts[0] == expectedUser && parts[1] == expectedPassword;
    }

}
