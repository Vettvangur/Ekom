using log4net;
using System.Web;
using Ekom.Services;

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
                if (_httpCtx != null)
                {
                    _httpCtx.Response.Cookies["StoreInfo"].Values["StoreAlias"] = value.Alias;
                }
                else
                {
                    _log.Info("Unable to change cookies for user when switching stores." +
                                              "HttpContext == null");
                }

                _store = value;
            }
        }

        public object Currency { get; set; }
        public string IPAddress { get; set; }
        public string DomainPrefix { get; set; }
        public Product Product { get; set; }
        public Category Category { get; set; }
        public User User { get; set; }
    }
}
