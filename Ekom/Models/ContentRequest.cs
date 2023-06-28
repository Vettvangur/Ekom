using Microsoft.AspNetCore.Http;

namespace Ekom.Models
{
    class ContentRequest
    {

        private IStore _store;
        private readonly HttpContext _httpCtx;
        public ContentRequest(HttpContext httpResponse)
        {
            _httpCtx = httpResponse;
        }
        public string IPAddress {
        
            get
            {
                var host = "";
                
                try
                {
                    host = _httpCtx.Request.Host.ToString();
                } catch
                {
                    // TODO
                }

                return host;
            }
        } 
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

            get { return _store; }
        }

        public object Currency { get; set; }
        public IProduct Product { get; set; }
        public ICategory Category { get; set; }
        public User User { get; set; }
    }
}
