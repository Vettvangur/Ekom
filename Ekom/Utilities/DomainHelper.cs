namespace Ekom.Utilities
{
    public static class DomainHelper
    {

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
