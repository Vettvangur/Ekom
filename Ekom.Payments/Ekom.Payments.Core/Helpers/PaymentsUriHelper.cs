using Microsoft.AspNetCore.Http;
using System;
using System.Web;

namespace Ekom.Payments.Helpers;

/// <summary>
/// URI Helper methods
/// </summary>
public static class PaymentsUriHelper
{
    /// <summary>
    /// Ensures param is full URI, otherwise adds components using data from Request
    /// </summary>
    /// <param name="uri">absolute or relative uri</param>
    /// <param name="Request"></param>
    /// <returns></returns>
    public static Uri EnsureFullUri(string uri, HttpRequest Request)
    {
        if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
        {
            return new Uri(uri);
        }
        else if (Uri.IsWellFormedUriString(uri, UriKind.Relative))
        {
            var basePath = $"{Request.Scheme}://{Request.Host}";

            return new Uri(basePath + uri);
        }

        throw new ArgumentException($"Uri \"{uri}\" is not a well formed Uri, please ensure correct configuration of urls used for success/error/cancel...", nameof(uri));
    }
    /// <summary>
    /// Ensures param is full URI, otherwise adds components using data from Request
    /// </summary>
    /// <param name="uri">absolute or relative uri</param>
    /// <param name="Request"></param>
    /// <returns></returns>
    public static Uri EnsureFullUri(Uri uri, HttpRequest Request)
    {
        if (uri.IsAbsoluteUri && uri.IsWellFormedOriginalString())
        {
            return uri;
        }
        else if (!uri.IsAbsoluteUri && uri.IsWellFormedOriginalString())
        {
            var basePath = $"{Request.Scheme}://{Request.Host}";

            return new Uri(basePath + uri);
        }

        throw new ArgumentException($"Uri \"{uri}\" is not a well formed Uri, please ensure correct configuration of urls used for success/error/cancel...", nameof(uri));
    }

    public static Uri AddQueryString(Uri? uri, string? queryString = "")
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }
        if (queryString == null)
        {
            throw new ArgumentNullException(nameof(queryString));
        }

        var qsNew = HttpUtility.ParseQueryString(
            queryString.StartsWith("?") ? queryString : "?" + queryString);

        if (string.IsNullOrEmpty(uri.Query))
        {
            return new Uri(uri + "?" + qsNew);
        }
        else
        {
            var qsOld = HttpUtility.ParseQueryString(uri.Query);
            foreach (var queryKey in qsOld.AllKeys)
            {
                foreach (var val in qsOld.GetValues(queryKey) ?? Array.Empty<string>())
                {
                    qsNew.Add(queryKey, val);
                }
            }

            return new Uri($"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}?{qsNew}");
        }
    }
}
