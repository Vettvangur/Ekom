using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace Ekom.Algolia.Services;

public interface IAlgoliaUserTokenProvider
{
    string? GetUserToken();
    string? GetOrCreateUserToken();
}

internal sealed class DefaultAlgoliaUserTokenProvider : IAlgoliaUserTokenProvider
{
    private const string CookieName = "ekom_algolia_user_token";
    private const string HttpContextItemKey = "Ekom.Algolia.UserToken";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public DefaultAlgoliaUserTokenProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetUserToken()
        => GetOrCreateUserToken();

    public string? GetOrCreateUserToken()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return null;

        if (context.Items.TryGetValue(HttpContextItemKey, out var item)
            && item is string itemToken
            && !string.IsNullOrWhiteSpace(itemToken))
        {
            return itemToken;
        }

        var cookieToken = context.Request.Cookies[CookieName];
        if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            context.Items[HttpContextItemKey] = cookieToken;
            return cookieToken;
        }

        var userToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        context.Items[HttpContextItemKey] = userToken;

        if (!context.Response.HasStarted)
        {
            context.Response.Cookies.Append(CookieName, userToken, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddMonths(6),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps
            });
        }

        return userToken;
    }
}
