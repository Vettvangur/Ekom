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

internal sealed class UrlService : IUrlService
{
    private readonly ILogger<UrlService> _logger;
    private readonly IUmbracoContextFactory _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IShortStringHelper _shortStringHelper;

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
    }

    public List<UmbracoUrl> BuildCategoryUrls(IEnumerable<UmbracoContent> categories, IStore store)
    {
        var categoryList = categories.ToList();
        var urls = new List<UmbracoUrl>();
        var rawSlug = categoryList.FirstOrDefault()?.GetRawValue("slug") ?? string.Empty;

        if (string.IsNullOrEmpty(rawSlug) || rawSlug == "#")
        {
            _logger.LogWarning("Slug is missing on category: {Category} Store: {Store}", categoryList.FirstOrDefault()?.Id, store.Alias);
            return urls;
        }

        var categoryProperty = rawSlug.IsJson()
            ? JsonConvert.DeserializeObject<PropertyValue>(rawSlug)
            : new PropertyValue { Type = PropertyEditorType.Store };

        if (categoryProperty != null && categoryProperty.Type == PropertyEditorType.Language && store.Domains.Any())
        {
            foreach (var domain in store.Domains)
            {
                var domainLanguage = domain.LanguageIsoCode;
                var basePath = CombineUrlParts(DomainHelper.GetDomainPrefix(domain.DomainName), store.UrlPrefix(domainLanguage));
                var slugs = new List<string>();
                var isValid = true;

                foreach (var category in categoryList)
                {
                    if (category.Properties.TryGetValue("ekmVirtualUrl", out var virtualFlag) && virtualFlag.IsBoolean())
                    {
                        continue;
                    }

                    var slug = category.GetValue("slug", domainLanguage);

                    if (string.IsNullOrWhiteSpace(slug))
                    {
                        isValid = false;
                        break;
                    }

                    slugs.Add(slug.ToUrlSegment(_shortStringHelper).AddTrailing());
                }

                if (!isValid || slugs.Count == 0)
                {
                    continue;
                }

                urls.Add(new UmbracoUrl
                {
                    Culture = domainLanguage,
                    Store = store.Alias,
                    Url = (basePath + string.Concat(slugs)).AddTrailing().EnsureStartsAndEndsWithChar('/'),
                    Domain = domain.DomainName,
                });
            }
        }
        else
        {
            foreach (var domain in store.Domains)
            {
                var builder = new StringBuilder("/");
                var hasMissingSlug = false;

                foreach (var category in categoryList)
                {
                    if (category.Properties.TryGetValue("ekmVirtualUrl", out var virtualFlag) && virtualFlag.IsBoolean())
                    {
                        continue;
                    }

                    var categorySlug = category.GetValue("slug", store.Alias);

                    if (string.IsNullOrWhiteSpace(categorySlug))
                    {
                        hasMissingSlug = true;
                        break;
                    }

                    builder.Append(categorySlug.ToUrlSegment(_shortStringHelper).AddTrailing());
                }

                if (hasMissingSlug)
                {
                    continue;
                }

                urls.Add(new UmbracoUrl
                {
                    Culture = domain.LanguageIsoCode,
                    Store = store.Alias,
                    Url = CombineUrlParts(DomainHelper.GetDomainPrefix(domain.DomainName), store.UrlPrefix(domain.LanguageIsoCode), builder.ToString()).EnsureStartsAndEndsWithChar('/'),
                    Domain = domain.DomainName,
                });
            }
        }

        return urls.DistinctBy(x => (x.Domain, x.Url, x.Store)).ToList();
    }

    [Obsolete]
    public IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store)
    {
        var urls = new HashSet<string>();

        if (string.IsNullOrEmpty(slug))
        {
            return urls;
        }

        foreach (var domain in store.Domains)
        {
            var builder = new StringBuilder(DomainHelper.GetDomainPrefix(domain.DomainName));

            foreach (var item in hierarchy)
            {
                builder.Append(item + "/");
            }

            var slugSafeAlias = slug.ToUrlSegment(_shortStringHelper);
            builder.Append(!string.IsNullOrEmpty(slugSafeAlias) ? slugSafeAlias : slug);

            urls.Add(builder.ToString().AddTrailing().ToLowerInvariant());
        }

        return urls.OrderBy(x => x.Length);
    }

    [Obsolete]
    public IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
    {
        var slug = item.GetRawValue("slug");

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException("Slug is missing on product: " + nodeId + " Store: " + store.Alias);
        }

        var slugValue = JsonConvert.DeserializeObject<PropertyValue>(slug);
        var urls = new HashSet<string>();
        var categoryUrls = categories.SelectMany(x => x.Urls);

        if (slugValue != null && slugValue.Type == PropertyEditorType.Language && store.Domains.Any())
        {
            foreach (var domain in store.Domains.DistinctBy(x => DomainHelper.GetDomainPrefix(x.DomainName)).ToList())
            {
                var domainPath = DomainHelper.GetDomainPrefix(domain.DomainName);
                var categoryUrl = categoryUrls.FirstOrDefault(x => x.InvariantStartsWith(domainPath));

                if (categoryUrl != null)
                {
                    urls.Add(categoryUrl + item.GetValue("slug", domain.LanguageIsoCode).ToUrlSegment(_shortStringHelper).AddTrailing().ToLowerInvariant());
                }
            }
        }
        else
        {
            foreach (var category in categories)
            {
                foreach (var categoryUrl in category.Urls)
                {
                    urls.Add(categoryUrl + item.GetValue("slug", store.Alias).ToUrlSegment(_shortStringHelper).AddTrailing().ToLowerInvariant());
                }
            }
        }

        return urls;
    }

    public List<UmbracoUrl> BuildProductUrlsWithContext(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
    {
        var rawSlug = item.GetRawValue("slug");

        if (string.IsNullOrWhiteSpace(rawSlug))
        {
            throw new InvalidOperationException($"Slug is missing on product: {nodeId} Store: {store.Alias}");
        }

        var slugValue = JsonConvert.DeserializeObject<PropertyValue>(rawSlug);
        var urls = new List<UmbracoUrl>();
        var categoryUrls = categories.SelectMany(c => c.UrlsWithContext).ToList();

        if (slugValue?.Type == PropertyEditorType.Language && store.Domains.Any())
        {
            foreach (var categoryUrl in categoryUrls)
            {
                var productSlug = GetPropertyValue(slugValue, categoryUrl.Culture);

                if (string.IsNullOrWhiteSpace(productSlug))
                {
                    continue;
                }

                urls.Add(new UmbracoUrl
                {
                    Culture = categoryUrl.Culture,
                    Store = store.Alias,
                    Url = (categoryUrl.Url + productSlug.ToUrlSegment(_shortStringHelper).AddTrailing()).EnsureStartsAndEndsWithChar('/'),
                    Domain = categoryUrl.Domain,
                });
            }
        }
        else
        {
            var productSlug = GetPropertyValue(slugValue, store.Alias, rawSlug);

            if (!string.IsNullOrWhiteSpace(productSlug))
            {
                var formattedSlug = productSlug.ToUrlSegment(_shortStringHelper).AddTrailing();

                foreach (var categoryUrl in categoryUrls)
                {
                    urls.Add(new UmbracoUrl
                    {
                        Culture = categoryUrl.Culture,
                        Store = store.Alias,
                        Url = (categoryUrl.Url + formattedSlug).EnsureStartsAndEndsWithChar('/'),
                        Domain = categoryUrl.Domain,
                    });
                }
            }
        }

        return urls;
    }

    private static string GetPropertyValue(PropertyValue? propertyValue, string key, string fallback = "")
    {
        if (propertyValue?.Values == null)
        {
            return fallback;
        }

        foreach (var value in propertyValue.Values)
        {
            if (value.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return value.Value?.ToString() ?? string.Empty;
            }
        }

        return fallback;
    }

    public string? GetNodeEntityUrl(INodeEntityWithUrl node)
    {
        var contextCategoryUrl = _httpContextAccessor.HttpContext?.Items[Configuration.EkmRequestKey] is Lazy<ContentRequest> lazyRequest
            && lazyRequest.Value?.Url is string urlFromRequest
            ? urlFromRequest
            : string.Empty;

        using var contextReference = _context.EnsureUmbracoContext();
        var publishedRequest = contextReference.UmbracoContext.PublishedRequest;
        var culture = publishedRequest?.Culture ?? CultureInfo.CurrentCulture.Name;
        var uri = publishedRequest?.Domain?.Uri ?? CookieHelper.GetUmbracoDomain(_httpContextAccessor.HttpContext?.Request.Cookies);
        var urlsWithContext = node.UrlsWithContext;
        var urls = node.Urls;

        if (uri == null && string.IsNullOrEmpty(contextCategoryUrl))
        {
            return urlsWithContext.FirstOrDefault(x => x.Culture == culture)?.Url;
        }

        if (!string.IsNullOrEmpty(contextCategoryUrl))
        {
            var match = urlsWithContext.FirstOrDefault(x => x.Culture == culture && x.Url.InvariantContains(contextCategoryUrl));

            if (match != null)
            {
                return match.Url;
            }
        }

        if (publishedRequest?.AbsolutePathDecoded is string absolutePathRaw)
        {
            var absolutePath = NormalizePath(absolutePathRaw);

            foreach (var prefix in BuildParentPrefixes(absolutePath))
            {
                var match = urlsWithContext.FirstOrDefault(x =>
                    x.Culture == culture &&
                    ContainsPathPrefix(NormalizePath(x.Url), prefix));

                if (match != null)
                {
                    return match.Url;
                }
            }
        }

        var matchByCulture = urlsWithContext.FirstOrDefault(x => x.Culture == culture);
        if (matchByCulture != null)
        {
            return matchByCulture.Url;
        }

        if (uri != null)
        {
            var pathPrefix = uri.AbsolutePath.AddTrailing();
            var matchByPrefix = urls.FirstOrDefault(x => x.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase));
            if (matchByPrefix != null)
            {
                return matchByPrefix;
            }
        }

        return urls.FirstOrDefault();
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        path = path.Trim();
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        while (path.Contains("//", StringComparison.Ordinal))
        {
            path = path.Replace("//", "/", StringComparison.Ordinal);
        }

        return path.EndsWith('/') ? path : path + "/";
    }

    private static IEnumerable<string> BuildParentPrefixes(string normalizedAbsolutePath)
    {
        yield return normalizedAbsolutePath;

        var current = normalizedAbsolutePath.TrimEnd('/');
        while (true)
        {
            var lastSlash = current.LastIndexOf('/');
            if (lastSlash <= 0)
            {
                break;
            }

            current = current[..lastSlash];
            yield return current + "/";
        }

        yield return "/";
    }

    private static bool ContainsPathPrefix(string urlNormalized, string prefixNormalized)
    {
        return urlNormalized.Contains(prefixNormalized, StringComparison.OrdinalIgnoreCase);
    }

    private static string CombineUrlParts(params string[] parts)
    {
        var cleanedParts = parts
            .Where(p => !string.IsNullOrWhiteSpace(p) && p != "/")
            .Select(p => p.Trim('/'))
            .Where(p => !string.IsNullOrEmpty(p));

        var joined = string.Join("/", cleanedParts);

        return "/" + joined.Trim('/') + (string.IsNullOrEmpty(joined) ? string.Empty : "/");
    }
}
