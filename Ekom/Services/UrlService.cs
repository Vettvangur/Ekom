using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Umbraco.Core;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;

namespace Ekom.Services
{
    static class UrlService
    {
        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

        /// <summary>
        /// Build URLs for category
        /// </summary>
        /// <param name="examineItems">All categories in hierarchy inclusive</param>
        /// <param name="store"></param>
        /// <returns>Collection of urls for all domains</returns>
        public static IEnumerable<string> BuildCategoryUrls(List<ISearchResult> examineItems, IStore store)
        {
            var urls = new HashSet<string>();

            foreach (var domain in store.Domains)
            {
                string domainPath = GetDomainPrefix(domain.DomainName);

                var builder = new StringBuilder(domainPath.AddTrailing());

                foreach (var examineItem in examineItems)
                {
                    var categorySlug = examineItem.GetStoreProperty("slug", store.Alias);
                    if (!string.IsNullOrWhiteSpace(categorySlug))
                        builder.Append(categorySlug.ToUrlSegment().AddTrailing());
                }

                var url = builder.ToString().AddTrailing().ToLower();

                urls.Add(url);
            }

            return urls;
        }

        /// <summary>
        /// Build category urls from a collection of parent slugs and the slug of observed category.
        /// Used f.x. by Wurth
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

                    var builder = new StringBuilder(domainPath.AddTrailing());

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

            return urls;
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

            return urls;
        }

        public static string GetDomainPrefix(string url)
        {
            // Handle domains w/ scheme
            bool _uriResult = Uri.TryCreate(url, UriKind.Absolute, out var uriResult);

            if (_uriResult)
            {
                return uriResult.AbsolutePath;
            }
            else
            {
                var firstIndexOf = url.IndexOf("/");

                return firstIndexOf > 0 ? url.Substring(firstIndexOf) : string.Empty;
            }
        }
    }
}
