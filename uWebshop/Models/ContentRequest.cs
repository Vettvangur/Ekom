using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core.Logging;

namespace uWebshop.Models
{
    public class ContentRequest
    {
        private Store _store;
        public Store Store {

            get { return _store; }

            set {
                // Make sure to update users cookies on store change
                var httpContext = HttpContext.Current;

                if (httpContext != null)
                {
                    httpContext.Response.Cookies["StoreInfo"].Values["StoreAlias"] = value.Alias;
                }
                else
                {
                    LogHelper.Info(GetType(), "Unable to change cookies for user when switching stores." + 
                                              "HttpContext == null");
                }

                _store = value;
            }
        }

        public object Currency { get; set; }
        public string DomainPrefix { get; set; }
        public Product Product { get; set; }
        public Category Category { get; set; }
    }
}
