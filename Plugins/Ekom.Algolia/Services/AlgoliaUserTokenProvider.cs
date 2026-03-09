using Microsoft.AspNetCore.Http;

namespace Ekom.Algolia.Services;

public interface IAlgoliaUserTokenProvider
{
    string? GetUserToken();
}

internal sealed class DefaultAlgoliaUserTokenProvider : IAlgoliaUserTokenProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DefaultAlgoliaUserTokenProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? GetUserToken()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return null;

        var userName = context.User?.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(userName))
            return userName;

        if (context.Session?.IsAvailable == true)
            return context.Session.Id;

        return context.TraceIdentifier;
    }
}
