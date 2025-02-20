using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

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
    private HttpContext _context;

    public EkomMiddleware(
        RequestDelegate next,
        ILogger<EkomMiddleware> logger,
        IUmbracoContextFactory umbracoContextFac,
        AppCaches appCaches,
        IMemberService memberService)
    {
        _next = next;
        _logger = logger;
        _umbracoContextFac = umbracoContextFac;
        _appCaches = appCaches;
        _memberService = memberService;
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context
    )
    {
        _context = context;

        await OnBeginRequest(_umbracoContextFac, _appCaches);
        await OnAuthenticateRequest(_appCaches, _memberService);

        await _next.Invoke(context);

        OnPostRequestHandlerExecute(_umbracoContextFac);
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
    private void OnPostRequestHandlerExecute(IUmbracoContextFactory umbracoContextFac)
    {
        try
        {
            using var umbCtx = umbracoContextFac.EnsureUmbracoContext();
            if (umbCtx?.UmbracoContext.PublishedRequest?.Domain?.Uri != null)
            {
                CookieHelper.SetUmbracoDomain(
                    _context.Response.Cookies,
                    umbCtx.UmbracoContext.PublishedRequest.Domain.Uri);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Http module PostRequestHandlerExecute failed, make sure to have domain set on the store root node.");
        }
    }

    private async Task OnBeginRequest(IUmbracoContextFactory umbracoContextFac, AppCaches appCaches)
    {
        try
        {
            if (_context?.Request == null)
            {
                return;
            }

            var requestPath = _context.Request?.Path.ToString();

            if (!AllowPath(requestPath))
            {
                return;
            }

            if (_context.RequestServices == null)
            {
                return;
            }

            if (umbracoContextFac == null)
            {
                return;
            }

            using var umbCtx = umbracoContextFac.EnsureUmbracoContext();

            if (umbCtx?.UmbracoContext != null)
            {
                IStore? store = null;

                if (_context?.Request != null)
                {
                    // Check for 'storeAlias' in the query string
                    var storeAlias = await GetStoreAliasFromRequest(_context.Request);
                    if (!string.IsNullOrEmpty(storeAlias))
                    {
                        store = API.Store.Instance.GetStore(storeAlias);
                    }
                }

                if (store != null)
                {
                    appCaches.RequestCache.Get("ekmRequest", () =>
                    new ContentRequest()
                    {
                        User = new User(),
                        Store = store
                    });
                } else
                {
                    appCaches.RequestCache.Get("ekmRequest", () =>
                    new ContentRequest()
                    {
                        User = new User()
                    });
                }


            }


        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Http module Begin Request failed");
        }
    }

    private async Task<string?> GetStoreAliasFromRequest(HttpRequest request)
    {
        try
        {
            if (request == null)
            {
                return null;
            }


            if (request.Query != null && request.Query.TryGetValue("storeAlias", out var storeAliasValue) && !string.IsNullOrEmpty(storeAliasValue))
            {
                return storeAliasValue;
            }

            // Check for storeAlias in form data
            if (request.HasFormContentType && request.Form.TryGetValue("storeAlias", out var storeAliasFormValue) && !string.IsNullOrEmpty(storeAliasFormValue))
            {
                return storeAliasFormValue;
            }

            if (request.Headers != null && request.Headers.TryGetValue("storeAlias", out var storeAliasHeaderValue) && !string.IsNullOrEmpty(storeAliasHeaderValue))
            {
                return storeAliasHeaderValue;
            }

            // Check for storeAlias in JSON body
            if (request.ContentType != null && request.ContentType.Contains("application/json", StringComparison.InvariantCultureIgnoreCase))
            {
                request.EnableBuffering(); // Allow reading the request body multiple times
                request.Body.Position = 0; // Rewind the body

                using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                request.Body.Position = 0; // Rewind the body for next middleware/controller

                if (!string.IsNullOrEmpty(body) && body.StartsWith('{') && body.Contains("storeAlias", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var options = new JsonDocumentOptions
                        {
                            AllowTrailingCommas = true // Handle slightly malformed JSON
                        };

                        var json = JsonDocument.Parse(body, options);

                        // Case-insensitive check for storeAlias
                        foreach (var property in json.RootElement.EnumerateObject())
                        {
                            if (string.Equals(property.Name, "storeAlias", StringComparison.OrdinalIgnoreCase))
                            {
                                return property.Value.GetString();
                            }
                        }
                    }
                    catch (JsonException)
                    {

                    }
                }
            }

            return null;

        } catch
        {
            return null;
        }
    }

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
        return true;
    }

    private async Task OnAuthenticateRequest(
        AppCaches appCaches,
        IMemberService memberService)
    {

        try
        {
            if (_context?.Request == null)
            {
                return;
            }

            var requestPath = "";

            try
            {
                if (_context.Request != null && _context.Request.Path != null)
                {
                    requestPath = _context.Request.Path.HasValue
                                  && _context.Request.Path.Value != "/null"
                        ? _context.Request.Path.Value
                        : string.Empty;
                }
                else
                {
                    requestPath = string.Empty; // Or log the issue for debugging
                }
            } catch
            {
                return;
            }

            if (!AllowPath(requestPath))
            {
                return;
            }

            if (_context.User?.Identity == null)
            {
                return;
            }

            if (_context.User.Identity.IsAuthenticated == false)
            {
                return;
            }

            var username = _context.User.Identity.Name;

            if (string.IsNullOrEmpty(username))
            {
                return;
            }

            if (appCaches.RequestCache.Get("ekmRequest", () => new ContentRequest()) is ContentRequest ekmRequest)
            {
                var memberContent = memberService.GetByUsername(username);

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
}
