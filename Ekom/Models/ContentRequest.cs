using Microsoft.AspNetCore.Http;

namespace Ekom.Models;

public class ContentRequest
{
    public string IPAddress { get; set; }
    public IStore? Store { get; set; }
    public object Currency { get; set; }
    public IProduct Product { get; set; }
    public ICategory Category { get; set; }
    public string Url { get; set; }
    public User User { get; set; }

    public void SetStoreCookie(string storeAlias, HttpContext? httpContext)
    {
        if (httpContext is null)
            return;

        httpContext.Response.Cookies.Append(
            "StoreInfo",
            "StoreAlias=" + storeAlias,
            new CookieOptions
            {
                Path = "/",
                SameSite = SameSiteMode.Lax,
                Secure = httpContext.Request.IsHttps,
                HttpOnly = false,
                Expires = DateTimeOffset.UtcNow.AddDays(365)
            });

        IPAddress = httpContext.Request.Host.ToString();
    }

}
