using Azure.Core;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
#else
using System.Web;
#endif
using System.Text.RegularExpressions;

namespace Ekom.Models
{
    class ContentRequest
    {

        private IStore _store;

#if NETCOREAPP
        private readonly HttpContext _httpCtx;
        public ContentRequest(HttpContext httpResponse)
        {
            _httpCtx = httpResponse;
        }
        public string IPAddress => _httpCtx.Request.Host.ToString();
        public IStore Store
        {
            set
            {
                _httpCtx.Response.Cookies.Append("StoreInfo", "StoreAlias=" + value.Alias);

                //// Make sure to update users cookies on store change
                //var legacyCookie = _httpCtx.Request.Cookies["StoreInfo"];
                //legacyCookie = Regex.Replace(legacyCookie, "(StoreAlias =)[^&]", value.Alias);
                //_httpCtx.Response.Cookies.Append("StoreInfo", legacyCookie);

                _store = value;
            }
#else
        private readonly HttpContextBase _httpCtx;
        public ContentRequest(HttpContextBase httpResponse)
        {
            _httpCtx = httpResponse;
        }
        public string IPAddress => _httpCtx.Request.UserHostAddress;
        public IStore Store
        {
            set
            {
                // Make sure to update users cookies on store change
                _httpCtx.Response.Cookies["StoreInfo"].Values["StoreAlias"] = value.Alias;

                _store = value;
            }
#endif

            get { return _store; }
        }

        public object Currency { get; set; }
        public IProduct Product { get; set; }
        public ICategory Category { get; set; }
        public User User { get; set; }
    }
}
