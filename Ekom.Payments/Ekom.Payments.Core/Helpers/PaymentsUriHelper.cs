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

    public static string AddQueryString(string uri, string queryString = "")
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }
        if (queryString == null)
        {
            throw new ArgumentNullException(nameof(queryString));
        }

        var u = new Uri(uri);
        var qsNew = HttpUtility.ParseQueryString(queryString);

        if (string.IsNullOrEmpty(u.Query))
        {
            return uri + "?" + qsNew;
        }
        else
        {
            var qsOld = HttpUtility.ParseQueryString(u.Query);
            foreach (var queryKey in qsOld.AllKeys)
            {
                foreach (var val in qsOld.GetValues(queryKey))
                {
                    qsNew.Add(queryKey, val);
                }
            }

            return $"{u.Scheme}://{u.Authority}{u.AbsolutePath}?{qsNew}";
        }
    }
}
