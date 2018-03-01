using log4net;
using System.Web;
using Ekom.Services;
using Ekom.Interfaces;

namespace Ekom.Models
{
    class ContentRequest
    {
        private ILog _log;
        private HttpContextBase _httpCtx;
        public ContentRequest(HttpContextBase httpContext, ILogFactory logFac)
        {
            _log = logFac.GetLogger(typeof(ContentRequest));
            _httpCtx = httpContext;
        }

        private Store _store;
        public Store Store
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
        public string DomainPrefix { get; set; }
        public IProduct Product { get; set; }
        public ICategory Category { get; set; }
        public User User { get; set; }
    }
}
