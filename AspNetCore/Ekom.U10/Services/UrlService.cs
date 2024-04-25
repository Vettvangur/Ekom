using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Umbraco.Cms.Core.Cache;
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
        /// <param name="items">All categories in hierarchy inclusive</param>
        /// <param name="store"></param>
        /// <returns>Collection of urls for all domains</returns>
        public List<UmbracoUrl> BuildCategoryUrls(IEnumerable<UmbracoContent> categories, IStore store)
        {
            var urls = new List<UmbracoUrl>();

            var categoryProperty = JsonConvert.DeserializeObject<PropertyValue>(categories.FirstOrDefault()?.GetValue("slug"));

            if (categoryProperty != null && categoryProperty.Type == PropertyEditorType.Language && store.Domains.Any())
            {
                foreach (var domain in store.Domains)
                {
                    var domainPath = DomainHelper.GetDomainPrefix(domain.DomainName);
                    
                    var builder = new StringBuilder(domainPath);
                    
                    foreach (var category in categories)
                    {
                        var virtualUrl = false;

                        if (category.Properties.TryGetValue("ekmVirtualUrl", out string _virtualUrl))
                        {
                            virtualUrl = _virtualUrl.IsBoolean();
                        }

                        if (virtualUrl)
                        {
                            continue;
                        }

                        var slug = category.GetValue("slug", domain.LanguageIsoCode);

                        if (!string.IsNullOrWhiteSpace(slug))
                            builder.Append(slug.ToUrlSegment(_shortStringHelper).AddTrailing());
                    }

                    var url = builder.ToString().AddTrailing().ToLower();

                    urls.Add(new UmbracoUrl()
                    {
                        Culture = domain.LanguageIsoCode,
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
                    var builder = new StringBuilder("/");

                    foreach (var category in categories)
                    {
                        var virtualUrl = false;

                        if (category.Properties.TryGetValue("ekmVirtualUrl", out string _virtualUrl))
                        {
                            virtualUrl = _virtualUrl.IsBoolean();
                        }

                        if (virtualUrl)
                        {
                            continue;
                        }
                        
                        var categorySlug = category.GetValue("slug", store.Alias);

                        if (!string.IsNullOrWhiteSpace(categorySlug))
                        {
                            builder.Append(categorySlug.ToUrlSegment(_shortStringHelper).AddTrailing());
                        }
                    }

                    var domainLastSement = UriHelper.GetLastSegment(domain.DomainName);

                    var url = domainLastSement + builder.ToString().AddTrailing().ToLower();

                    urls.Add(new UmbracoUrl()
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
            var slug = item.GetValue("slug");

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
            var slug = item.GetValue("slug");

            if (string.IsNullOrWhiteSpace(slug))
            {
                throw new Exception("Slug is missing on product: " + nodeId + " Store: " + store.Alias);
            }

            var slugValue = JsonConvert.DeserializeObject<PropertyValue>(slug);

            var urls = new List<UmbracoUrl>();

            var categoryUrls = categories.SelectMany(x => x.UrlsWithContext);

            if (slugValue != null && slugValue.Type == PropertyEditorType.Language && store.Domains.Any())
            {
                foreach (var categoryUrl in categoryUrls)
                {
                    var productSlug = item.GetValue("slug", categoryUrl.Culture);

                    var url = categoryUrl.Url + productSlug.ToUrlSegment(_shortStringHelper).AddTrailing().ToLower();

                    urls.Add(new UmbracoUrl()
                    {
                        Culture = categoryUrl.Culture,
                        Store = store.Alias,
                        Url = url,
                        Domain = categoryUrl.Domain
                    });
                }
            }
            else
            {

                foreach (var category in categories)
                {
                    foreach (var categoryUrlContext in category.UrlsWithContext)
                    {
                        var productUrl = item.GetValue("slug", store.Alias);

                        if (string.IsNullOrEmpty(productUrl))
                        {
                            continue;
                        }

                        var url = categoryUrlContext.Url + productUrl.ToUrlSegment(_shortStringHelper).AddTrailing().ToLower();

                        urls.Add(new UmbracoUrl()
                        {
                            Culture = categoryUrlContext.Culture,
                            Store = store.Alias,
                            Url = url,
                            Domain = categoryUrlContext.Domain
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
        public string GetNodeEntityUrl(INodeEntityWithUrl node)
        {
            // Urls is a list of relative urls.
            // Umbraco cultures & hostnames can include a prefix
            // This code matches to find correct prefix,
            // aside from that, relative urls should be similar between domains

            string contextCategoryUrl = string.Empty;

            var requestCacheFromHttpContext = _httpContextAccessor.HttpContext?.Items["ekmRequest"] as Lazy<ContentRequest>;
            if (requestCacheFromHttpContext != null)
            {
                if (requestCacheFromHttpContext.Value.Url != null)
                {
                    contextCategoryUrl = requestCacheFromHttpContext.Value.Url;
                }
            }

            using (var cref = _context.EnsureUmbracoContext())
            {
                var pubReq = cref.UmbracoContext.PublishedRequest;
                var culture = System.Threading.Thread.CurrentThread.CurrentCulture.Name;
                var debugLogging = false;
                Uri uri = null;
                if (pubReq == null || pubReq?.PublishedContent == null)
                {

                    var httpCtx = _httpContextAccessor.HttpContext;
                    if (httpCtx != null)
                    {
                        uri = CookieHelper.GetUmbracoDomain(httpCtx.Request.Cookies);

                        // This could happen when background service calls an api on the store end.
                        // Cookie needs to be sent with the api request.

                        if (uri == null)
                        {
                            uri = pubReq?.Domain?.Uri;
                        }

                    }
                }
                else
                {
                    uri = pubReq.Domain?.Uri;
                    culture = pubReq.Culture;
                }

                if (uri == null)
                {
                    return node.Urls.FirstOrDefault();
                }

                var path = uri
                    .AbsolutePath
                    .ToLower()
                    .AddTrailing();

                if (!string.IsNullOrEmpty(contextCategoryUrl))
                {
                    var nodeUrl = node.UrlsWithContext.FirstOrDefault(x => x.Culture == culture && x.Url.InvariantContains(contextCategoryUrl));

                    if (nodeUrl != null)
                    {
                        return nodeUrl.Url;
                    }
                }

                if (pubReq != null)
                {
                    var nodeUrl = node.UrlsWithContext.FirstOrDefault(x => x.Culture == culture && x.Url.InvariantContains(pubReq.AbsolutePathDecoded));

                    if (nodeUrl != null)
                    {
                        return nodeUrl.Url;
                    }
                }
 
                if (node.UrlsWithContext.Any(x => x.Culture == culture))
                {
                    return node.UrlsWithContext.FirstOrDefault(x => x.Culture == culture)?.Url;
                }

                var findUrlByPrefix = node.Urls
                    .FirstOrDefault(x => x.StartsWith(path));

                return findUrlByPrefix ?? node.Urls.FirstOrDefault();
            }
        }
    }
}
