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


        public static string GetLastSegment(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";

            // Handle URLs without a scheme by checking for a scheme, and prepending a fake one if missing.
            if (!url.Contains("://"))
                url = "http://" + url;

            Uri uri;
            try
            {
                uri = new Uri(url, UriKind.Absolute);
            }
            catch (UriFormatException)
            {
                return "";
            }

            // Ensure the path ends with a slash for consistency
            string path = uri.AbsolutePath.TrimEnd('/');

            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            // Find the last segment
            int lastIndex = path.LastIndexOf("/", StringComparison.Ordinal);

            // Extract the segment including the last "/"
            string lastSegment = path.Substring(lastIndex);

            return lastSegment;
        }

    }
}
