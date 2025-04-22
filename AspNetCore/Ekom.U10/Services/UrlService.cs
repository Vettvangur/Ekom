using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

class UrlService : IUrlService
{
    readonly ILogger _logger;
    readonly IUmbracoContextFactory _context;
    readonly IHttpContextAccessor _httpContextAccessor;
    readonly IShortStringHelper _shortStringHelper;
    readonly AppCaches _appCaches;

    public UrlService(
        ILogger<UrlService> logger,
        IUmbracoContextFactory context,
        IHttpContextAccessor httpContextAccessor,
        IShortStringHelper shortStringHelper,
        AppCaches appCaches)
    {
        _logger = logger;
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _shortStringHelper = shortStringHelper;
        _appCaches = appCaches;
    }

    /// <summary>
    /// Build URLs for category
    /// </summary>
    /// <param name="categories">All categories in hierarchy inclusive</param>
    /// <param name="store"></param>
    /// <returns>Collection of urls for all domains</returns>
    public List<UmbracoUrl> BuildCategoryUrls(IEnumerable<UmbracoContent> categories, IStore store)
    {
        var urls = new List<UmbracoUrl>();

        var rawSlug = categories.FirstOrDefault()?.GetRawValue("slug") ?? "";

        if (string.IsNullOrEmpty(rawSlug) || rawSlug == "#")
        {
            _logger.LogWarning("Slug is missing on category: {category} Store: {store}", categories.FirstOrDefault()?.Id, store.Alias);
            return urls;
        }

        var categoryProperty = JsonConvert.DeserializeObject<PropertyValue>(rawSlug);

        if (categoryProperty != null && categoryProperty.Type == PropertyEditorType.Language && store.Domains.Any())
        {
            foreach (var domain in store.Domains)
            {
                var domainLang = domain.LanguageIsoCode;
                var domainPath = DomainHelper.GetDomainPrefix(domain.DomainName);
                var storeUrlPrefix = store.UrlPrefix(domainLang);

                var slugs = new List<string>();
                var isValid = true;

                foreach (var category in categories)
                {
                    // Skip virtual categories
                    if (category.Properties.TryGetValue("ekmVirtualUrl", out string virtualFlag) &&
                        virtualFlag.IsBoolean())
                    {
                        continue;
                    }

                    var slug = category.GetValue("slug", domainLang);

                    if (string.IsNullOrWhiteSpace(slug))
                    {
                        isValid = false;
                        break; // exit early — not valid for this domain
                    }

                    slugs.Add(slug.ToUrlSegment(_shortStringHelper).AddTrailing());
                }

                if (!isValid || slugs.Count == 0)
                    continue;

                // Combine domain path + store prefix + slugs
                var basePath = CombineUrlParts(domainPath, storeUrlPrefix);
                var url = (basePath + string.Concat(slugs)).AddTrailing();

                urls.Add(new UmbracoUrl
                {
                    Culture = domainLang,
                    Store = store.Alias,
                    Url = url,
                    Domain = domain.DomainName
                });
            }
        }
        else
        {
            foreach (var domain in store.Domains)
            {
                var domainLang = domain.LanguageIsoCode;
                var domainPath = DomainHelper.GetDomainPrefix(domain.DomainName);
                var storeUrlPrefix = store.UrlPrefix(domainLang);

                var builder = new StringBuilder("/");
                var hasMissingSlug = false;

                foreach (var category in categories)
                {
                    // Skip virtual categories
                    if (category.Properties.TryGetValue("ekmVirtualUrl", out string virtualFlag) &&
                        virtualFlag.IsBoolean())
                    {
                        continue;
                    }

                    var categorySlug = category.GetValue("slug", store.Alias);

                    if (string.IsNullOrWhiteSpace(categorySlug))
                    {
                        hasMissingSlug = true;
                        break; // stop processing this domain
                    }

                    builder.Append(categorySlug.ToUrlSegment(_shortStringHelper).AddTrailing());
                }

                if (hasMissingSlug)
                    continue;

                var url = CombineUrlParts(domainPath, storeUrlPrefix, builder.ToString());

                urls.Add(new UmbracoUrl
                {
                    Culture = domain.LanguageIsoCode,
                    Store = store.Alias,
                    Url = url,
                    Domain = domain.DomainName
                });
            }
        }

        return urls.DistinctBy(x => (x.Domain, x.Url, x.Store)).ToList();
    }

    /// <summary>
    /// Build category urls from a collection of parent slugs and the slug of observed category.
    /// Used for category creation at runtime f.x.
    /// </summary>
    /// <param name="slug">Short name of category</param>
    /// <param name="hierarchy">Ordered list of slugs for all parents</param>
    /// <param name="store"></param>
    /// <returns>Collection of urls for all domains</returns>
    [Obsolete]
    public IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store)
    {
        var urls = new HashSet<string>();

        if (!string.IsNullOrEmpty(slug))
        {
            foreach (var domain in store.Domains)
            {
                string domainPath = DomainHelper.GetDomainPrefix(domain.DomainName);

                var builder = new StringBuilder(domainPath);

                foreach (var item in hierarchy)
                {
                    builder.Append(item + "/");
                }

                var slugSafeAlias = slug.ToUrlSegment(_shortStringHelper);
                if (!string.IsNullOrEmpty(slugSafeAlias))
                {
                    builder.Append(slugSafeAlias);
                }
                else
                {
                    builder.Append(slug);
                }

                var url = builder.ToString().AddTrailing().ToLower();

                urls.Add(url);
            }
        }

        // ordering by length ensures that publishedRequests with the default / prefix
        // do not match more specific prefixes such as /is/
        return urls.OrderBy(x => x.Length);
    }

    [Obsolete]
    public IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
    {
        var slug = item.GetRawValue("slug");

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new Exception("Slug is missing on product: " + nodeId + " Store: " + store.Alias);
        }

        var slugValue = JsonConvert.DeserializeObject<PropertyValue>(slug);

        var urls = new HashSet<string>();

        var categoryUrls = categories.SelectMany(x => x.Urls);

        if (slugValue != null && slugValue.Type == PropertyEditorType.Language && store.Domains.Any())
        {
            foreach (var domain in store.Domains.DistinctBy(x => DomainHelper.GetDomainPrefix(x.DomainName)).ToList())
            {
                string domainPath = DomainHelper.GetDomainPrefix(domain.DomainName);

                var categoryUrl = categoryUrls.FirstOrDefault(x => x.InvariantStartsWith(domainPath));

                if (categoryUrl != null)
                {
                    var productSlug = "";

                    productSlug = item.GetValue("slug", domain.LanguageIsoCode);

                    var url = categoryUrl + productSlug.ToUrlSegment(_shortStringHelper).AddTrailing().ToLower();

                    urls.Add(url);
                }
            }
        } else
        {

            foreach (var category in categories)
            {
                foreach (var categoryUrl in category.Urls)
                {
                    var url = categoryUrl + item.GetValue("slug", store.Alias).ToUrlSegment(_shortStringHelper).AddTrailing().ToLower();

                    urls.Add(url);
                }
            }
        }

        // Categories order by length, otherwise we mess up primary category priority
        return urls /*.OrderBy(x => x.Length) */;
    }

    public List<UmbracoUrl> BuildProductUrlsWithContext(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
    {
        var rawSlug = item.GetRawValue("slug");

        if (string.IsNullOrWhiteSpace(rawSlug))
            throw new Exception($"Slug is missing on product: {nodeId} Store: {store.Alias}");

        var slugValue = JsonConvert.DeserializeObject<PropertyValue>(rawSlug);
        var urls = new List<UmbracoUrl>();
        var categoryUrls = categories.SelectMany(c => c.UrlsWithContext);

        if (slugValue?.Type == PropertyEditorType.Language && store.Domains.Any())
        {
            foreach (var categoryUrl in categoryUrls)
            {
                var productSlug = item.GetValue("slug", categoryUrl.Culture);

                if (string.IsNullOrWhiteSpace(productSlug))
                    continue;

                var fullUrl = (categoryUrl.Url + productSlug.ToUrlSegment(_shortStringHelper).AddTrailing());

                urls.Add(new UmbracoUrl
                {
                    Culture = categoryUrl.Culture,
                    Store = store.Alias,
                    Url = fullUrl,
                    Domain = categoryUrl.Domain
                });
            }
        }
        else
        {
            var productSlug = item.GetValue("slug", store.Alias);

            if (!string.IsNullOrWhiteSpace(productSlug))
            {
                var formattedSlug = productSlug.ToUrlSegment(_shortStringHelper).AddTrailing();

                foreach (var categoryUrl in categoryUrls)
                {
                    urls.Add(new UmbracoUrl
                    {
                        Culture = categoryUrl.Culture,
                        Store = store.Alias,
                        Url = categoryUrl.Url + formattedSlug,
                        Domain = categoryUrl.Domain
                    });
                }
            }
        }

        return urls;
    }

    /// <summary>
    /// If we need to refactor this further see
    /// Umbraco.Web.Routing.DomainUtilities.GetCultureFromDomains
    /// for inspiration
    /// </summary>
    public string? GetNodeEntityUrl(INodeEntityWithUrl node)
    {
        var contextCategoryUrl = _httpContextAccessor.HttpContext?.Items[Configuration.EkmRequestKey] is Lazy<ContentRequest> lazyRequest
            && lazyRequest.Value?.Url is string urlFromRequest
            ? urlFromRequest
            : string.Empty;

        using var cref = _context.EnsureUmbracoContext();
        var pubReq = cref.UmbracoContext.PublishedRequest;

        var culture = pubReq?.Culture ?? CultureInfo.CurrentCulture.Name;
        var uri = pubReq?.Domain?.Uri ?? CookieHelper.GetUmbracoDomain(_httpContextAccessor.HttpContext?.Request.Cookies);

        var urlsWithContext = node.UrlsWithContext;
        var urls = node.Urls;

        // Fallback if nothing useful is available
        if (uri == null && string.IsNullOrEmpty(contextCategoryUrl))
        {
            return urlsWithContext.FirstOrDefault(x => x.Culture == culture)?.Url;
        }

        // Match against current category context
        if (!string.IsNullOrEmpty(contextCategoryUrl))
        {
            var match = urlsWithContext.FirstOrDefault(x =>
                x.Culture == culture && x.Url.InvariantContains(contextCategoryUrl));

            if (match != null)
                return match.Url;
        }

        // Match against Umbraco request path
        if (pubReq?.AbsolutePathDecoded is string absolutePath)
        {
            var match = urlsWithContext.FirstOrDefault(x =>
                x.Culture == culture && x.Url.InvariantContains(absolutePath));

            if (match != null)
                return match.Url;
        }

        // Fallback: any URL with matching culture
        var matchByCulture = urlsWithContext.FirstOrDefault(x => x.Culture == culture);
        if (matchByCulture != null)
        {
            return matchByCulture.Url;
        }

        // Try matching by domain path prefix
        if (uri != null)
        {
            var pathPrefix = uri.AbsolutePath.AddTrailing();
            var matchByPrefix = urls.FirstOrDefault(x => x.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase));
            if (matchByPrefix != null)
            {
                return matchByPrefix;
            }
        }

        // Final fallback
        return urls.FirstOrDefault();
    }

    public static string CombineUrlParts(params string[] parts)
    {
        var cleanedParts = parts
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "/")
            .Select(p => p.Trim('/'))
            .Where(p => !string.IsNullOrEmpty(p));

        var joined = string.Join("/", cleanedParts);

        return "/" + joined.Trim('/') + (string.IsNullOrEmpty(joined) ? "" : "/");
    }



}
