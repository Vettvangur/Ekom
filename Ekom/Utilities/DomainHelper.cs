namespace Ekom.Utilities;

public static class DomainHelper
{
    public static string GetDomainPrefix(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        url = url.AddTrailing();

        // Handle relative URLs like "/" or "/en"
        if (url.StartsWith("/"))
        {
            return url;
        }

        // Handle URLs with ports (e.g., http://example.com:8080/en)
        if (url.Contains(":") && url.IndexOf(":", StringComparison.Ordinal) > 5)
        {
            int index = url.IndexOf("/", url.IndexOf(":", StringComparison.Ordinal), StringComparison.Ordinal);
            if (index >= 0)
            {
                return url.Substring(index).AddTrailing();
            }
        }

        // Try to parse absolute URL
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uriAbsoluteResult))
        {
            return uriAbsoluteResult.AbsolutePath.AddTrailing();
        }

        // Fallback for unparseable but valid strings like "example.com/en"
        int firstIndexOf = url.IndexOf("/", StringComparison.Ordinal);
        return firstIndexOf > 0 ? url.Substring(firstIndexOf).AddTrailing() : string.Empty;
    }

}
