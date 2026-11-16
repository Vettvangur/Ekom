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
        if (Uri.TryCreate(uri, UriKind.Absolute, out var absoluteUri) && absoluteUri.IsWellFormedOriginalString())
        {
            return absoluteUri;
        }
        else if (Uri.TryCreate(uri, UriKind.Relative, out var relativeUri) && relativeUri.IsWellFormedOriginalString())
        {
            var basePath = $"{Request.Scheme}://{Request.Host}";

            return CreateUri(basePath + relativeUri, uri, Request, "relative");
        }

        throw CreateInvalidUriException(uri, Request, "absolute or relative");
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

            return CreateUri(basePath + uri, uri.ToString(), Request, "relative");
        }

        throw CreateInvalidUriException(uri.ToString(), Request, uri.IsAbsoluteUri ? "absolute" : "relative");
    }

    static Uri CreateUri(string uri, string configuredUri, HttpRequest request, string uriKind)
    {
        try
        {
            return new Uri(uri, UriKind.Absolute);
        }
        catch (UriFormatException ex)
        {
            throw CreateInvalidUriException(configuredUri, request, uriKind, uri, ex);
        }
    }

    static ArgumentException CreateInvalidUriException(
        string uri,
        HttpRequest request,
        string uriKind,
        string? resolvedUri = null,
        Exception? innerException = null)
        => new ArgumentException(
            $"Uri \"{uri}\" is not a well formed {uriKind} Uri. " +
            $"Request scheme: \"{request.Scheme}\". Request host: \"{request.Host}\". " +
            (resolvedUri == null ? string.Empty : $"Resolved Uri: \"{resolvedUri}\". ") +
            "Please ensure correct configuration of urls used for success/error/cancel...",
            nameof(uri),
            innerException);

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
