using Ekom.Interfaces;
using Examine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.IO;
using Umbraco.Core.Models.PublishedContent;

namespace Ekom.Utilities
{
    public static class UrlHelper
    {
        /// <summary>
        /// Build URLs for category
        /// </summary>
        /// <param name="examineItems">All categories in hierarchy inclusive</param>
        /// <param name="store"></param>
        /// <returns>Collection of urls for all domains</returns>
        public static IEnumerable<string> BuildCategoryUrls(List<ISearchResult> examineItems, IStore store)
        {
            var urls = new HashSet<string>();

            if (store.Domains != null)
            {
                var domains = store.Domains.Select(domain => GetDomainPrefix(domain.DomainName)).DistinctBy(x => x).ToList();

                foreach (var domainPath in domains)
                {
                    var builder = new StringBuilder(domainPath);

                    foreach (var examineItem in examineItems)
                    {
                        var categorySlug = examineItem.GetStoreProperty("slug", store.Alias);
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

                foreach (var examineItem in examineItems)
                {
                    var categorySlug = examineItem.GetStoreProperty("slug", store.Alias);
                    if (!string.IsNullOrWhiteSpace(categorySlug))
                    {
                        builder.Append(categorySlug.ToUrlSegment().AddTrailing());
                    }
                }

                var url = builder.ToString().AddTrailing().ToLower();

                urls.Add(url);
            }

            return urls.DistinctBy(x => x).OrderBy(x => x.Length);
        }

        /// <summary>
        /// Build category urls from a collection of parent slugs and the slug of observed category.
        /// Used for category creation at runtime f.x.
        /// </summary>
        /// <param name="slug">Short name of category</param>
        /// <param name="hierarchy">Ordered list of slugs for all parents</param>
        /// <param name="store"></param>
        /// <returns>Collection of urls for all domains</returns>
        public static IEnumerable<string> BuildCategoryUrls(string slug, List<string> hierarchy, IStore store)
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

        public static IEnumerable<string> BuildProductUrls(string slug, IEnumerable<ICategory> categories, IStore store)
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

        public static string GetDomainPrefix(string url)
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
    }
}
