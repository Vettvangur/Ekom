namespace Ekom.Klaviyo.Helpers;

internal static class UrlBuilder
{
    public static string Combine(string host, string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return "";
        }

        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must be provided", nameof(host));

        if (string.IsNullOrWhiteSpace(url))
            return host.TrimEnd('/');

        return $"{host.TrimEnd('/')}/{url.TrimStart('/')}";
    }
}
