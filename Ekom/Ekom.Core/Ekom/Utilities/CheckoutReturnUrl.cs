using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Ekom.Utilities;

internal static class CheckoutReturnUrl
{
    private static readonly HashSet<string> ResultParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "errorStatus", "errorReason", "errorMessage", "canRetry", "errorType", "orderline",
    };

    // Return URLs are navigation targets within this store. Provider redirect URLs are separate.
    internal static bool TryNormalize(string? value, HttpRequest? request, out string returnUrl)
    {
        returnUrl = "/";
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var decoded = Uri.UnescapeDataString(value);
        if (decoded.Any(c => char.IsControl(c) || c is '\\' or '<' or '>' or '\"' or '\'') ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (value.StartsWith("~/", StringComparison.Ordinal))
        {
            value = (request?.PathBase.Value ?? string.Empty) + value[1..];
        }
        else if (!value.StartsWith('/'))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var absolute) || !IsSameOrigin(absolute, request))
            {
                return false;
            }

            value = absolute.PathAndQuery + absolute.Fragment;
        }

        if (!value.StartsWith('/') || Uri.UnescapeDataString(value).StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var fragmentIndex = value.IndexOf('#', StringComparison.Ordinal);
        var fragment = fragmentIndex >= 0 ? value[fragmentIndex..] : string.Empty;
        var pathAndQuery = fragmentIndex >= 0 ? value[..fragmentIndex] : value;
        var queryIndex = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var path = queryIndex >= 0 ? pathAndQuery[..queryIndex] : pathAndQuery;
        var parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (queryIndex >= 0)
        {
            foreach (var pair in QueryHelpers.ParseQuery(pathAndQuery[queryIndex..]))
            {
                // Checkout controls these fields; discard all supplied occurrences before adding results.
                if (ResultParameters.Contains(pair.Key)) continue;
                if (pair.Value.Count != 1) return false;
                parameters.Add(pair.Key, pair.Value[0]);
            }
        }

        returnUrl = QueryHelpers.AddQueryString(path, parameters) + fragment;
        return true;
    }

    private static bool IsSameOrigin(Uri absolute, HttpRequest? request)
    {
        return request != null &&
            Uri.TryCreate($"{request.Scheme}://{request.Host}/", UriKind.Absolute, out var origin) &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps) &&
            string.Equals(absolute.Scheme, origin.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(absolute.Host, origin.Host, StringComparison.OrdinalIgnoreCase) &&
            absolute.Port == origin.Port && string.IsNullOrEmpty(absolute.UserInfo);
    }
}
