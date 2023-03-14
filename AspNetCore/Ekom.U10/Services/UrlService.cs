using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb.Services
{
    class UrlService : IUrlService
    {
        readonly ILogger _logger;
        readonly IUmbracoContextFactory _context;
        readonly IHttpContextAccessor _httpContextAccessor;
        readonly IShortStringHelper _shortStringHelper;

        public UrlService(
            ILogger<UrlService> logger,
            IUmbracoContextFactory context,
            IHttpContextAccessor httpContextAccessor,
            IShortStringHelper shortStringHelper)
        {
            _logger = logger;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _shortStringHelper = shortStringHelper;
        }

        /// <summary>
        /// Build URLs for category
        /// </summary>
        /// <param name="items">All categories in hierarchy inclusive</param>
        /// <param name="store"></param>
        /// <returns>Collection of urls for all domains</returns>
        public IEnumerable<string> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store)
        {
            var urls = new HashSet<string>();

            if (store.Domains != null)
            {
                foreach (var domain in store.Domains.DistinctBy(x => GetDomainPrefix(x.DomainName)).ToList())
                {
                    var domainPath = GetDomainPrefix(domain.DomainName);
                    
                    var builder = new StringBuilder(domainPath);
                    
                    foreach (var item in items)
                    {
                        string slug = "";
                        var slugValue = JsonConvert.DeserializeObject<PropertyValue>(item.GetValue("slug"));

                        if (slugValue.Type == PropertyEditorType.Language)
                        {
                            slug = item.GetValue("slug", domain.LanguageIsoCode);
                        } else if (slugValue.Type == PropertyEditorType.Store)
                        {
                            slug = item.GetValue("slug", store.Alias);
                        }
                        
                        if (!string.IsNullOrWhiteSpace(slug))
                            builder.Append(slug.ToUrlSegment(_shortStringHelper).AddTrailing());
                    }

                    var url = builder.ToString().AddTrailing().ToLower();

                    urls.Add(url);
                }
            }
            else
            {
                var builder = new StringBuilder("/");

                foreach (var item in items)
                {
                    var categorySlug = item.GetValue("slug", store.Alias);

                    if (!string.IsNullOrWhiteSpace(categorySlug))
                    {
                        builder.Append(categorySlug.ToUrlSegment(_shortStringHelper).AddTrailing());
                    }
                }

                var url = builder.ToString().AddTrailing().ToLower();

                urls.Add(url);
            }

            return urls.Distinct().OrderBy(x => x.Length);
        }

        /// <summary>
        /// Build category urls from a collection of parent slugs and the slug of observed category.
        /// Used for category creation at runtime f.x.
        /// </summary>
        /// <param name="slug">Short name of category</param>
        /// <param name="hierarchy">Ordered list of slugs for all parents</param>
        /// <param name="store"></param>
        /// <returns>Collection of urls for all domains</returns>
        public IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store)
        {
            var urls = new HashSet<string>();

            if (!string.IsNullOrEmpty(slug))
            {
                foreach (var domain in store.Domains)
                {
                    string domainPath = GetDomainPrefix(domain.DomainName);

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

        public IEnumerable<string> BuildProductUrls(UmbracoContent item, IEnumerable<ICategory> categories, IStore store, int nodeId)
        {
            var slug = item.GetValue("slug");

            if (string.IsNullOrWhiteSpace(slug))
            {
                throw new Exception("Slug is missing on product: " + nodeId + " Store: " + store.Alias);
            }
            
            var urls = new HashSet<string>();

            var categoryUrls = categories.SelectMany(x => x.Urls);

            if (store.Domains != null)
            {
                foreach (var domain in store.Domains.DistinctBy(x => GetDomainPrefix(x.DomainName)).ToList())
                {
                    string domainPath = GetDomainPrefix(domain.DomainName);

                    var categoryUrl = categoryUrls.FirstOrDefault(x => x.InvariantStartsWith(domainPath));

                    if (categoryUrl != null)
                    {
                        var productSlug = "";

                        var slugValue = JsonConvert.DeserializeObject<PropertyValue>(slug);

                        if (slugValue.Type == PropertyEditorType.Language)
                        {
                            productSlug = item.GetValue("slug", domain.LanguageIsoCode);
                        }
                        else if (slugValue.Type == PropertyEditorType.Store)
                        {
                            productSlug = item.GetValue("slug", store.Alias);
                        }

                        var url = categoryUrl + productSlug.ToUrlSegment(_shortStringHelper).AddTrailing().ToLower();

                        urls.Add(url);
                    }
                }
            }

            // Categories order by length, otherwise we mess up primary category priority
            return urls /*.OrderBy(x => x.Length) */;
        }

        public string GetDomainPrefix(string url)
        {
            url = url.AddTrailing();

            if (url.Contains(":") && url.IndexOf(":", StringComparison.Ordinal) > 5)
            {
                url = url.Substring(url.IndexOf("/", StringComparison.Ordinal));

                return url;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uriAbsoluteResult))
            {
                return uriAbsoluteResult.AbsolutePath.AddTrailing();
            }

            var firstIndexOf = url.IndexOf("/", StringComparison.Ordinal);

            return firstIndexOf > 0 ? url.Substring(firstIndexOf).AddTrailing() : string.Empty;
        }

        /// <summary>
        /// If we need to refactor this further see
        /// Umbraco.Web.Routing.DomainUtilities.GetCultureFromDomains
        /// for inspiration
        /// </summary>
        public string GetNodeEntityUrl(INodeEntityWithUrl node)
        {
            // Urls is a list of relative urls.
            // Umbraco cultures & hostnames can include a prefix
            // This code matches to find correct prefix,
            // aside from that, relative urls should be similar between domains

            using (var cref = _context.EnsureUmbracoContext())
            {
                var pubReq = cref.UmbracoContext.PublishedRequest;

                var debugLogging = false;
                Uri uri = null;
                if (pubReq == null)
                {

                    var httpCtx = _httpContextAccessor.HttpContext;
                    if (httpCtx != null)
                    {
                        uri = CookieHelper.GetUmbracoDomain(httpCtx.Request.Cookies);

                        // This could happen when background service calls an api on the store end.
                        // Cookie needs to be sent with the api request.

                        if (uri == null)
                        {
                            debugLogging = true;
                        }

                    }
                }
                else
                {
                    uri = pubReq.Domain?.Uri;
                }

                if (uri == null)
                {
                    var message = "Unable to determine umbraco domain." + (cref.UmbracoContext == null ? "Umbraco Context is null, the Url getter is not supported in background services." : "") + (pubReq != null && pubReq.Domain == null ? "Domain is not found in context. Check if domain is set in culture and hostnames under the store root node" : "") + (pubReq == null ? "Publish request is null. Fallbacking to cookie did not work." : "") + " - " + new System.Diagnostics.StackTrace();

                    // Handle when umbraco couldn't find matching domain for request
                    // This can be due to the following error message, or some failure with the cookie solution
                    // Historically this would happen when ajaxing to surface controllers without including the ufprt ctx
                    // today the cookie solution should cover that as well

                    if (debugLogging)
                    {
                        _logger.LogDebug(message);
                    }
                    else
                    {               
                        // This was error, but on swapping in some projects the log got filled with it.
                        // We dont know at the moment why the context is not available but it does not seem to affect the site.

                        _logger.LogDebug(
                            message);
                    }

                    return node.Urls.FirstOrDefault();
                }

                var path = uri
                    .AbsolutePath
                    .ToLower()
                    .AddTrailing();

                var findUrlByPrefix = node.Urls
                    .FirstOrDefault(x => x.StartsWith(path));

                return findUrlByPrefix ?? node.Urls.FirstOrDefault();
            }
        }
    }
}
