using Microsoft.AspNetCore.Http;

namespace Ekom.Models
{
    public class ContentRequest
    {
        public string IPAddress { get; set; }
        public IStore Store { get; set; }
        public object Currency { get; set; }
        public IProduct Product { get; set; }
        public ICategory Category { get; set; }
        public string Url { get; set; }
        public User User { get; set; }

        public void SetStoreCookie(string storeAlias, HttpContext? httpContext)
        {
            if (httpContext != null)
            {
                var cookies = httpContext.Response.Cookies;
                cookies?.Append("StoreInfo", "StoreAlias=" + storeAlias);

                IPAddress = httpContext.Request.Host.ToString();
            }

        }

    }
}
