using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Web;

namespace Ekom.Umb;

/// <summary>
/// Creates the per-request Ekom context and resolves store/member details for Ekom requests.
/// </summary>
internal sealed class EkomMiddleware
{
    private const string EkmRequestKey = "ekmRequest";

    private readonly RequestDelegate _next;
    private readonly ILogger<EkomMiddleware> _logger;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly AppCaches _appCaches;
    private readonly IMemberService _memberService;
    private readonly IStoreService _storeService;

    public EkomMiddleware(
        RequestDelegate next,
        ILogger<EkomMiddleware> logger,
        IUmbracoContextFactory umbracoContextFactory,
        AppCaches appCaches,
        IMemberService memberService,
        IStoreService storeService)
    {
        _next = next;
        _logger = logger;
        _umbracoContextFactory = umbracoContextFactory;
        _appCaches = appCaches;
        _memberService = memberService;
        _storeService = storeService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        OnBeginRequest(context);
        await OnAuthenticateRequest(context);

        await _next.Invoke(context);

        OnPostRequestHandlerExecute(context);
    }

    private void OnPostRequestHandlerExecute(HttpContext context)
    {
        try
        {
            using var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();
            if (umbracoContextReference.UmbracoContext.PublishedRequest?.Domain?.Uri != null)
            {
                CookieHelper.SetUmbracoDomain(
                    context.Response.Cookies,
                    umbracoContextReference.UmbracoContext.PublishedRequest.Domain.Uri,
                    context.Request.IsHttps);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ekom PostRequestHandlerExecute failed; make sure a domain is set on the store root node.");
        }
    }

    private void OnBeginRequest(HttpContext http)
    {
        try
        {
            var request = http.Request;
            if (!AllowPath(request.Path))
            {
                return;
            }

            var isEkomApiRoute = request.Path.StartsWithSegments("/ekom", StringComparison.OrdinalIgnoreCase);
            IStore? store = null;

            if (isEkomApiRoute)
            {
                var alias = GetStoreAliasFromRequest(request);

                if (!string.IsNullOrEmpty(alias))
                {
                    try
                    {
                        store = _storeService.GetStoreByAlias(alias);
                    }
                    catch (StoreNotFoundException ex)
                    {
                        _logger.LogDebug(ex, "Store with alias {Alias} not found for request to {Path}", alias, request.Path);
                    }
                }
            }
            else
            {
                var host = request.Host.ToString();
                var basePath = string.Concat(host, FirstPathSegment(request.Path));

                store = _storeService.GetStoreByDomain(basePath);
            }

            using var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();

            var ekmRequest = _appCaches.RequestCache.Get(EkmRequestKey, () => new ContentRequest
            {
                User = new User(),
            }) as ContentRequest;

            if (ekmRequest is null)
            {
                return;
            }

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
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return "/";
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var trimmed = path.TrimEnd('/');
        var nextSlash = trimmed.IndexOf('/', 1);

        return nextSlash < 0
            ? trimmed + "/"
            : trimmed[..(nextSlash + 1)];
    }

    private string? GetStoreAliasFromRequest(HttpRequest request)
    {
        try
        {
            if (request.Query.TryGetValue("storeAlias", out var storeAliasValue) && !string.IsNullOrEmpty(storeAliasValue))
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

            if (request.Headers.TryGetValue("storeAlias", out var storeAliasHeaderValue) && !string.IsNullOrEmpty(storeAliasHeaderValue))
            {
                return storeAliasHeaderValue;
            }

            if (request.Cookies.TryGetValue("StoreInfo", out var storeInfoCookieValue) && !string.IsNullOrEmpty(storeInfoCookieValue))
            {
                var decodedCookie = Uri.UnescapeDataString(storeInfoCookieValue);
                var parts = decodedCookie.Split('=', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 2 && parts[0].Equals("StoreAlias", StringComparison.InvariantCultureIgnoreCase))
                {
                    return parts[1];
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool AllowPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        if (path.StartsWith("/umbraco/surface", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/umbraco/api", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/umbraco/backoffice/api", StringComparison.InvariantCultureIgnoreCase))
        {
            return true;
        }

        if (path.StartsWith("/umbraco/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/media/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/app_plugins/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/build/", StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        return !Path.HasExtension(path);
    }

    private async Task OnAuthenticateRequest(HttpContext context)
    {
        try
        {
            var requestPath = context.Request.Path.HasValue && context.Request.Path.Value != "/null"
                ? context.Request.Path.Value
                : string.Empty;

            if (!AllowPath(requestPath))
            {
                return;
            }

            var authentication = await IsAuthenticated(context);

            if (!authentication.IsAuthenticated)
            {
                return;
            }

            if (_appCaches.RequestCache.Get(EkmRequestKey, () => new ContentRequest()) is not ContentRequest ekmRequest)
            {
                return;
            }

            var memberContent = _memberService.GetByUsername(authentication.Username);

            if (memberContent == null)
            {
                return;
            }

            ekmRequest.User = new User
            {
                Email = memberContent.Email,
                Username = memberContent.UserName,
                UserId = memberContent.Id,
                Name = memberContent.Name,
            };

            if (!string.IsNullOrEmpty(memberContent.OrderId) && Guid.TryParse(memberContent.OrderId, out var orderId))
            {
                ekmRequest.User.OrderId = orderId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AuthenticateRequest failed.");
        }
    }

    public async Task<(bool IsAuthenticated, string Username)> IsAuthenticated(HttpContext context)
    {
        var username = context.User.Identity?.IsAuthenticated == true
            ? context.User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                ?? context.User.Identity?.Name
            : null;

        if (string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(context.Request.Headers.Authorization))
        {
            var authResult = await context.AuthenticateAsync("OpenIddict.Validation.AspNetCore");

            if (authResult.Succeeded && authResult.Principal != null)
            {
                username = authResult.Principal.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
                    ?? authResult.Principal.Identity?.Name;
            }
        }

        return (!string.IsNullOrEmpty(username), username ?? string.Empty);
    }
}
