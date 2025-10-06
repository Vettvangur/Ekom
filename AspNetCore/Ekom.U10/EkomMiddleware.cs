using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Web;

namespace Ekom.Umb;

/// <summary>
/// Ekom middleware, ensures an ekmRequest object exists in the runtime cache for all
/// controller requests. <br />
/// The module checks for existence of a store querystring parameter and if found,
/// creates an ekmRequest object with DomainPrefix and currency if applicable. <br />
/// <br />
/// ConventionalMiddleware https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/extensibility?view=aspnetcore-6.0
/// </summary>
class EkomMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EkomMiddleware> _logger;
    private readonly IUmbracoContextFactory _umbracoContextFac;
    private readonly AppCaches _appCaches;
    private readonly IMemberService _memberService;
    private readonly IStoreService _storeService;
    public EkomMiddleware(
        RequestDelegate next,
        ILogger<EkomMiddleware> logger,
        IUmbracoContextFactory umbracoContextFac,
        AppCaches appCaches,
        IMemberService memberService,
        IStoreService storeService)
    {
        _next = next;
        _logger = logger;
        _umbracoContextFac = umbracoContextFac;
        _appCaches = appCaches;
        _memberService = memberService;
        _storeService = storeService;
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context
    )
    {
        OnBeginRequest(_umbracoContextFac, _appCaches, context);
        await OnAuthenticateRequest(_appCaches, _memberService, context);

        await _next.Invoke(context);

        OnPostRequestHandlerExecute(_umbracoContextFac, context);
    }

    /// <summary>
    /// We store the requests umbraco domain in a cookie
    /// This ensures that ajax requests with no ufprt form value 
    /// can still resolve the correct urls for a product.
    /// See Url property on Product/Category.
    /// 
    /// Another option would have been to always return the list of urls for a product/category,
    /// leaving it to the frontend to match, sub-par solution but simpler?
    /// </summary>
    private void OnPostRequestHandlerExecute(IUmbracoContextFactory umbracoContextFac, HttpContext context)
    {
        try
        {
            using var umbCtx = umbracoContextFac.EnsureUmbracoContext();
            if (umbCtx?.UmbracoContext.PublishedRequest?.Domain?.Uri != null)
            {
                CookieHelper.SetUmbracoDomain(
                    context.Response.Cookies,
                    umbCtx.UmbracoContext.PublishedRequest.Domain.Uri);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Http module PostRequestHandlerExecute failed, make sure to have domain set on the store root node.");
        }
    }

    private const string EkmRequestKey = "ekmRequest";

    private void OnBeginRequest(
        IUmbracoContextFactory umbracoContextFactory,
        AppCaches appCaches,
        HttpContext http)
    {
        try
        {
            if (http?.Request is not { } req) return;
            if (!AllowPath(req.Path)) return;

            // Skip domain-based store resolution for /ekom routes
            bool isEkomRoute = req.Path.StartsWithSegments("/ekom", StringComparison.OrdinalIgnoreCase);

            // 1) Querystring override takes precedence
            var qsAlias = GetStoreAliasFromRequest(req);
            IStore? store = null;

            if (!string.IsNullOrWhiteSpace(qsAlias))
            {
                store = _storeService.GetStoreByAlias(qsAlias);
            }
            // 2) Only try domain mapping when NOT under /ekom
            else if (!isEkomRoute && store is null)
            {
                var host = req.Host.ToString();
                var basePath = string.Concat(host, FirstPathSegment(req.Path));
                var culture = CultureInfo.CurrentCulture.Name;

                store = _storeService.GetStoreByDomain(basePath, culture);
            }

            using var _ = umbracoContextFactory?.EnsureUmbracoContext();

            // Create/get the per-request payload once
            var ekmRequest = appCaches.RequestCache.Get(EkmRequestKey, () => new ContentRequest
            {
                User = new User()
                // Store set below if we resolved one
            }) as ContentRequest;

            if (ekmRequest is null) return;

            // When on /ekom with no alias, keep whatever was already in the request cache
            if (store is not null && ekmRequest.Store?.Alias != store.Alias)
            {
                ekmRequest.Store = store;
                ekmRequest.SetStoreCookie(store.Alias, http);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BeginRequest failed.");
        }
    }
    private static string FirstPathSegment(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return "/";

        // Ensure leading slash; trim trailing slashes for processing
        if (!path.StartsWith("/")) path = "/" + path;
        var trimmed = path.TrimEnd('/');

        // Find the next slash after the first character
        var nextSlash = trimmed.IndexOf('/', 1);
        if (nextSlash < 0)
        {
            // Single segment like "/page" -> ensure trailing slash
            return trimmed + "/";
        }

        // Multiple segments: keep "/page/" only
        return trimmed.Substring(0, nextSlash + 1);
    }

    private string? GetStoreAliasFromRequest(HttpRequest request)
    {
        try
        {
            if (request.Query != null && request.Query.TryGetValue("storeAlias", out var storeAliasValue) && !string.IsNullOrEmpty(storeAliasValue))
            {
                return storeAliasValue;
            }

            if (request.ContentType?.Contains("application/x-www-form-urlencoded", StringComparison.InvariantCultureIgnoreCase) == true)
            {
                try
                {
                    request.EnableBuffering();

                    if (request.Form.TryGetValue("storeAlias", out var storeAliasFormValue) && !string.IsNullOrEmpty(storeAliasFormValue))
                    {
                        return storeAliasFormValue;
                    }

                    request.Body.Position = 0;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read storeAlias from request.Form");
                }
            }

            if (request.Headers != null && request.Headers.TryGetValue("storeAlias", out var storeAliasHeaderValue) && !string.IsNullOrEmpty(storeAliasHeaderValue))
            {
                return storeAliasHeaderValue;
            }

            if (request.Cookies != null && request.Cookies.TryGetValue("StoreInfo", out var storeInfoCookieValue) && !string.IsNullOrEmpty(storeInfoCookieValue))
            {
                var decodedCookie = Uri.UnescapeDataString(storeInfoCookieValue); // decode %3D to '=' etc.

                var parts = decodedCookie.Split('=', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[0].Equals("StoreAlias", StringComparison.InvariantCultureIgnoreCase))
                {
                    return parts[1];
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }


    //private async Task<string> GetRawBodyStringAsync(HttpRequest request,
    //                                                    bool enableBuffering = false,
    //                                                    Encoding encoding = null,
    //                                                    Stream inputStream = null)
    //{
    //    if (encoding == null)
    //        encoding = Encoding.UTF8;

    //    if (inputStream == null)
    //    {
    //        if (enableBuffering)
    //            request.EnableBuffering();
    //        inputStream = request.Body;
    //    }

    //    string? bodyString = string.Empty;
    //    using (var reader = new StreamReader(inputStream,
    //        encoding,
    //        detectEncodingFromByteOrderMarks: false,
    //        leaveOpen: enableBuffering))
    //    {
    //        try
    //        {
    //            bodyString = await reader.ReadToEndAsync();
    //        }
    //        catch (Exception)
    //        {
    //            bodyString = string.Empty;
    //        }

    //        if (inputStream.CanSeek)
    //            inputStream.Position = 0;
    //    }

    //    return bodyString;
    //}

    private bool AllowPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (
            path.StartsWith("/umbraco/surface", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/umbraco/api", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/umbraco/backoffice/api", StringComparison.InvariantCultureIgnoreCase)
        )
        {
            return true;
        }

        if (
            path.StartsWith("/umbraco/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/media/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/app_plugins/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/build/", StringComparison.InvariantCultureIgnoreCase)
        )
        {
            return false;
        }

        // Reject any path that ends with a file extension
        if (Path.HasExtension(path))
        {
            return false;
        }

        return true;
    }

    private async Task OnAuthenticateRequest(
        AppCaches appCaches,
        IMemberService memberService,
        HttpContext context)
    {

        try
        {


            if (context?.Request == null)
            {
                return;
            }

            var requestPath = "";

            try
            {
                if (context.Request != null && context.Request.Path != null)
                {
                    requestPath = context.Request.Path.HasValue
                                  && context.Request.Path.Value != "/null"
                        ? context.Request.Path.Value
                        : string.Empty;
                }
                else
                {
                    requestPath = string.Empty; // Or log the issue for debugging
                }
            }
            catch
            {
                return;
            }

            if (!AllowPath(requestPath))
            {
                return;
            }

            var isAuthenticated = await IsAuthenticated(context);

            if (!isAuthenticated.IsAuthenticated)
            {
                return;
            }

            if (appCaches.RequestCache.Get("ekmRequest", () => new ContentRequest()) is ContentRequest ekmRequest)
            {
                var memberContent = memberService.GetByUsername(isAuthenticated.Username);

                if (memberContent != null)
                {
                    ekmRequest.User = new User
                    {
                        Email = memberContent.Email,
                        Username = memberContent.UserName,
                        UserId = memberContent.Id,
                        Name = memberContent.Name,
                    };

                    var orderid = memberContent.OrderId;

                    if (!string.IsNullOrEmpty(orderid) && Guid.TryParse(orderid, out var guid))
                    {
                        ekmRequest.User.OrderId = guid;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            //_logger.LogError(ex, "AuthenticateRequest Failed");
        }
    }


    public async Task<(bool IsAuthenticated, string Username)> IsAuthenticated(HttpContext context)
    {
        string? username = context.User.Identity?.IsAuthenticated == true
            ? context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                ?? context.User.Identity?.Name
            : null;

        if (string.IsNullOrEmpty(username))
        {
            var authorizationHeaderValue = context.Request.Headers.Authorization;
            if (!string.IsNullOrEmpty(authorizationHeaderValue))
            {
                var authResult = await context.AuthenticateAsync("OpenIddict.Validation.AspNetCore");

                if (authResult.Succeeded && authResult.Principal != null)
                {
                    username = authResult.Principal.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                        ?? authResult.Principal.Identity?.Name;
                }
            }
        }

        return (!string.IsNullOrEmpty(username), username ?? string.Empty);
    }

}
