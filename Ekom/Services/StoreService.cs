using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Models;
using Microsoft.AspNetCore.Http;
using System.Globalization;

namespace Ekom.Services;

class StoreService : IStoreService
{
    private readonly IStoreDomainCache _domainCache;
    private readonly IBaseCache<IStore> _storeCache;
    private readonly HttpContext _httpContext;

    /// <summary>
    /// ctor
    /// </summary>
    public StoreService(
        IStoreDomainCache domainCache,
        IBaseCache<IStore> storeCache,
        IHttpContextAccessor httpContextAccessor)
    {
        _domainCache = domainCache;
        _storeCache = storeCache;
        _httpContext = httpContextAccessor.HttpContext;
    }

    public IStore? GetStoreByDomain(string domain = "", string? culture = null)
    {
        static string TrimEndSlash(string s) => (s ?? string.Empty).Trim().TrimEnd('/');

        static string Normalize(string s)
        {
            // No trailing slashes, case-insensitive compare target
            var t = TrimEndSlash(s).ToLowerInvariant();
            // Make both "/seg" and "seg" comparable by also providing alternatives in candidates
            return t;
        }

        static (string host, int? port, string firstSeg) ParseHostPortFirstSeg(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return (string.Empty, null, string.Empty);

            if (!input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                input = "http://" + input; // allow bare "host/seg"
            }

            if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            {
                var host = uri.Host;
                int? port = uri.IsDefaultPort ? null : uri.Port;

                var firstSeg = uri.AbsolutePath
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault() ?? string.Empty;

                return (host, port, firstSeg);
            }

            return (string.Empty, null, string.Empty);
        }

        UmbracoDomain? FindDomain(string key)
        {
            var norm = Normalize(key);
            return _domainCache.Cache
                .Select(kv => kv.Value)
                .FirstOrDefault(d =>
                {
                    var dom = Normalize(d.DomainName);

                    // If the stored domain is relative (starts with '/'), compare against the *path* candidates only.
                    var domIsRelative = d.DomainName?.StartsWith("/", StringComparison.Ordinal) == true;

                    if (domIsRelative)
                    {
                        // Accept both "/seg" and "seg" equivalence
                        var keyNoLeading = norm.StartsWith("/") ? norm[1..] : norm;
                        var domNoLeading = dom.StartsWith("/") ? dom[1..] : dom;
                        return string.Equals(norm, dom, StringComparison.InvariantCultureIgnoreCase)
                            || string.Equals(keyNoLeading, domNoLeading, StringComparison.InvariantCultureIgnoreCase);
                    }

                    // Otherwise, compare full normalized strings
                    return string.Equals(dom, norm, StringComparison.InvariantCultureIgnoreCase);
                });
        }

        IStore? FindStoreForDomain(UmbracoDomain storeDomain, string requestedCulture)
        {
            var store = _storeCache.Cache
                .Select(kv => kv.Value)
                .FirstOrDefault(s => s.StoreRootNodeId == storeDomain.RootContentId
                                     && s.Culture != null
                                     && s.Culture.Name.Equals(requestedCulture, StringComparison.OrdinalIgnoreCase));

            store ??= _storeCache.Cache
                .Select(kv => kv.Value)
                .FirstOrDefault(s => s.StoreRootNodeId == storeDomain.RootContentId);

            return store;
        }

        // -------- main --------
        var (host, port, firstSeg) = ParseHostPortFirstSeg(domain);

        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(host))
        {
            if (port.HasValue)
            {
                var withPort = $"{host}:{port.Value}";
                if (!string.IsNullOrWhiteSpace(firstSeg))
                    candidates.Add($"{withPort}/{firstSeg}");
                candidates.Add(withPort);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(firstSeg))
                    candidates.Add($"{host}/{firstSeg}");
                candidates.Add(host);
            }
        }

        // ⬇️ NEW: fall back to relative culture domains
        if (!string.IsNullOrWhiteSpace(firstSeg))
        {
            candidates.Add("/" + firstSeg); // matches entries saved like "/nespresso-finland-en"
            candidates.Add(firstSeg);       // matches entries saved without the leading slash
        }

        IStore? store = null;
        foreach (var c in candidates)
        {
            var dom = FindDomain(c);
            if (dom == null) continue;

            var requestedCulture = string.IsNullOrWhiteSpace(culture) ? dom.LanguageIsoCode : culture;

            store = FindStoreForDomain(dom, requestedCulture!);
            if (store != null) break;
        }

        store ??= GetAllStores().FirstOrDefault();
        return store ?? throw new Exception("No store found in cache.");
    }

    public IStore? GetStoreByAlias(string? alias)
    {
        if (!_storeCache.Cache.Any())
        {
            throw new StoreNotFoundException("Unable to find any stores!");
        }

        if (!_storeCache.Cache.Any(x => string.Equals(alias, x.Value.Alias, StringComparison.InvariantCultureIgnoreCase)))
        {
            throw new StoreNotFoundException($"Unable to find any store: {alias}");
        }

        IStore store = _storeCache.Cache
                         .FirstOrDefault(x => string.Equals(alias, x.Value.Alias, StringComparison.InvariantCultureIgnoreCase))
                         .Value;

        // If store is not found by alias then return first store
        return store ?? _storeCache.Cache.FirstOrDefault().Value
            ?? throw new StoreNotFoundException("Unable to find any stores!");
    }

    public IStore? GetStoreFromCache()
    {

        if (_httpContext != null && _httpContext.Items != null && _httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out object? ekmRequestObject))
        {
            ContentRequest? contentRequest = null;

            // Check for Lazy<ContentRequest>
            if (ekmRequestObject is Lazy<ContentRequest> lazyContentRequest)
            {
                contentRequest = lazyContentRequest.Value;
            }
            // Check for Lazy<object> and cast to ContentRequest
            else if (ekmRequestObject is Lazy<object> lazyObject && lazyObject.Value is ContentRequest contentRequestFromObject)
            {
                contentRequest = contentRequestFromObject;
            }

            if (contentRequest != null && contentRequest.Store != null)
            {
                // Use contentRequest as needed
                return contentRequest.Store;
            }
        }

        return GetAllStores().FirstOrDefault();
    }

    public IStore? SetStore(string storeAlias)
    {
        if (string.IsNullOrEmpty(storeAlias))
        {
            return null;
        }

        // Retrieve the store by its alias
        IStore? store = GetStoreByAlias(storeAlias);

        // If no store is found, return null
        if (store == null)
        {
            return null;
        }

        if (_httpContext != null)
        {
            if (_httpContext.Items.TryGetValue(Configuration.EkmRequestKey, out object? ekmRequestObject) &&
                ekmRequestObject is Lazy<ContentRequest> lazyRequest)
            {
                // Access Lazy.Value to ensure it initializes
                ContentRequest contentRequest = lazyRequest.Value;
                if (contentRequest != null)
                {
                    contentRequest.Store = store;
                }
            }
            else
            {
                // Create and initialize a new Lazy<ContentRequest>
                Lazy<ContentRequest> newContentRequest = new Lazy<ContentRequest>(() =>
                {
                    ContentRequest request = new ContentRequest();
                    request.Store = store;
                    return request;
                });

                // Add to HttpContext.Items
                _httpContext.Items[Configuration.EkmRequestKey] = newContentRequest;

                // Access Lazy.Value to ensure initialization
                _ = newContentRequest.Value;
            }
        }

        // Return the store
        return store;
    }

    public IEnumerable<IStore> GetAllStores()
    {
        return _storeCache.Cache.Select(x => x.Value).OrderBy(x => x.SortOrder);
    }

    public IEnumerable<UmbracoDomain> GetDomains()
    {
        return _domainCache.Cache.Select(x => x.Value);
    }
}
