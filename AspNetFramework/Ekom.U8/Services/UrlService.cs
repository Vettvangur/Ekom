using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Web;

namespace Ekom.U8.Services
{
    class UrlService : IUrlService
    {
        readonly ILogger _logger;
        readonly IUmbracoContextFactory _context;
        readonly HttpContextBase _httpContext;
        /// <summary>
        /// Build URLs for category
        /// </summary>
        /// <param name="examineItems">All categories in hierarchy inclusive</param>
        /// <param name="store"></param>
        /// <returns>Collection of urls for all domains</returns>
        public IEnumerable<string> BuildCategoryUrls(IEnumerable<UmbracoContent> items, IStore store)
        {
            var urls = new HashSet<string>();

            if (store.Domains != null)
            {
                var domains = store.Domains.Select(domain => GetDomainPrefix(domain.DomainName)).Distinct().ToList();

                foreach (var domainPath in domains)
                {
                    var builder = new StringBuilder(domainPath);

                    foreach (var item in items)
                    {
                        var categorySlug = item.GetStoreProperty("slug", store.Alias);
                        if (!string.IsNullOrWhiteSpace(categorySlug))
                            builder.Append(categorySlug.ToUrlSegment().AddTrailing());
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
                    var categorySlug = item.GetStoreProperty("slug", store.Alias);
                    if (!string.IsNullOrWhiteSpace(categorySlug))
                    {
                        builder.Append(categorySlug.ToUrlSegment().AddTrailing());
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

                    var slugSafeAlias = slug.ToUrlSegment();
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

        public IEnumerable<string> BuildProductUrls(string slug, IEnumerable<ICategory> categories, IStore store)
        {
            var urls = new HashSet<string>();

            foreach (var category in categories)
            {
                foreach (var categoryUrl in category.Urls)
                {
                    var url = categoryUrl + slug.ToUrlSegment().AddTrailing().ToLower();

                    urls.Add(url);
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

                    if (_httpContext != null)
                    {
                        uri = CookieHelper.GetUmbracoDomain(_httpContext.Request.Cookies);

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
                        _logger.Debug<NodeService>(message);
                    }
                    else
                    {
                        _logger.Error<NodeService>(message);
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
