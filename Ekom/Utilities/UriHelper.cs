using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Utilities
{
    static class UriHelper
    {
        public static string EnsureFullUri(string uri, Uri requestUrl)
        {
            if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
            {
                return uri;
            }
            else if (Uri.IsWellFormedUriString(uri, UriKind.Relative))
            {
                var url = requestUrl;
                var basePath = $"{url.Scheme}://{url.Authority}";

                return basePath + uri;
            }

            throw new ArgumentException($"Uri \"{uri}\" is not a well formed Uri, please ensure correct configuration of urls used for success/error/cancel...", nameof(uri));
        }
    }
}
