using Ekom.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Ekom.Utilities
{
    public class CookieHelper
    {
        public static CurrencyModel GetCurrencyCookieValue(List<CurrencyModel> currencies, string storeAlias)
        {
            try
            {
                if (HttpContext.Current != null && HttpContext.Current.Request != null)
                {
                    var cookie = HttpContext.Current.Request.Cookies["EkomCurrency-" + storeAlias];

                    if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                    {
                        var c = currencies.FirstOrDefault(x => x.CurrencyValue == cookie.Value);

                        if (c != null)
                        {
                            return c;
                        }
                    }

                }
            }
            catch
            {

            }

            return currencies.FirstOrDefault();

        }
    }
}
