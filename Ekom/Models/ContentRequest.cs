using log4net;
using System.Web;
using Ekom.Services;
using Ekom.Interfaces;

namespace Ekom.Models
{
    class ContentRequest
    {
        private ILogger _logger;
        private HttpContextBase _httpCtx;
        public ContentRequest(HttpContextBase httpContext, ILogger log)
        {
            _logger = logger;
            _httpCtx = httpContext;
        }

        private IStore _store;
        public IStore Store
        {
            get { return _store; }

            set
            {
                // Make sure to update users cookies on store change
                _httpCtx.Response.Cookies["StoreInfo"].Values["StoreAlias"] = value.Alias;

                _store = value;
            }
        }

        public object Currency { get; set; }
        public string IPAddress => _httpCtx.Request.UserHostAddress;
        public IProduct Product { get; set; }
        public ICategory Category { get; set; }
        public User User { get; set; }
    }
}
