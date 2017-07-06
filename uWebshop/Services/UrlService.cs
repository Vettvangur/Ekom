using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Models;
using Umbraco.Web;
using uWebshop.Utilities;
using Examine;
using uWebshop.Helpers;
using Umbraco.Core;
using uWebshop.Interfaces;

namespace uWebshop.Services
{
    public static class UrlService
    {
        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

        public static List<string> BuildCategoryUrl(string slug, IEnumerable<SearchResult> examineItems, Store store)
        {
            var urls = new List<string>();

            foreach (var domain in store.Domains)
            {
                StringBuilder builder = new StringBuilder();

                string domainPath = GetDomainPrefix(domain.DomainName);

                foreach (var examineItem in examineItems)
                {
                    string categorySlug = examineItem.GetStoreProperty("slug", store.Alias);

                    builder.AppendFormat("{0}/{1}", domainPath, categorySlug.ToSafeAlias());
                }

                var url = builder.ToString().AddTrailing().ToLower();

                urls.Add(url);
            }

            return urls;
        }

        public static List<string> BuildProductUrls(string slug, IEnumerable<ICategory> categories, Store store)
        {
            var urls = new List<string>();

            foreach (var category in categories)
            {
                foreach (var categoryUrl in category.Urls)
                {
                    var url = categoryUrl + slug.ToSafeAlias().AddTrailing().ToLower();

                    urls.Add(url);
                }
            }

            return urls;
        }

        public static string GetDomainPrefix(string url)
        {
            var domainPath = string.Empty;

            if (url.IndexOf("/") > 0)
            {
                var firstIndexOf = url.IndexOf("/");

                domainPath = url.Substring(firstIndexOf, url.Length - firstIndexOf);
            }

            return domainPath;
        }
    }
}
