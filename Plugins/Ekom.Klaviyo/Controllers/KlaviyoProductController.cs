using Ekom.Klaviyo.Enrichers.ProductFeedEnricher;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Catalog;
using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
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

    public KlaviyoProductController(
        IOptions<KlaviyoOptions> opt,
        IMemoryCache cache,
        KlaviyoProductFeedEnrichmentPipeline pipeline)
    {
        _opt = opt.Value;
        _cache = cache;
        _pipeline = pipeline;
    }

    [HttpGet("feed")]
    [Produces("application/json")]
    public async Task<IActionResult> GetProductFeedAsync([FromQuery] string? storeAlias = null, CancellationToken ct = default)
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

        var cacheKey = $"klaviyo:feed:v2:{storeAlias}";

        var json = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(60);
            entry.Priority = CacheItemPriority.High;

            var productsResponse = await API.Catalog.Instance.GetAllProductsAsync(storeAlias, ct: ct);
            var products = productsResponse?.Products;

            var feed = new List<KlaviyoProductFeedItem>();

            if (products is not null)
            {
                foreach (var product in products.Where(HasProductImage))
                {
                    var item = product.ToKlaviyoProductFeedItem(_opt);

                    await _pipeline.ApplyAsync(
                        item,
                        new KlaviyoProductFeedEnrichmentContext
                        {
                            StoreAlias = storeAlias,
                            Product = product,
                            FeedItem = item,
                            Options = _opt
                        },
                        ct);

                    feed.Add(item);
                }
            }

            var jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            return JsonSerializer.Serialize(feed, jsonOptions);
        });

        Response.Headers.CacheControl = "private, max-age=3600";

        if (!string.IsNullOrEmpty(_opt.Catalog.Username) || !string.IsNullOrEmpty(_opt.Catalog.Password))
        {
            Response.Headers.Vary = "Authorization";
        }
          
        return Content(json ?? "", "application/json");
    }

    private static bool HasProductImage(IProduct product)
    {
        return !string.IsNullOrWhiteSpace(product.Images?.FirstOrDefault()?.Url);
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
