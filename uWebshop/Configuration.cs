using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop
{
    public class Configuration
    {
        public static string ExamineSearcher
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsExamineSearcher"];

                return !string.IsNullOrEmpty(value) ? value : "ExternalSearcher";
            }
        }

        public static bool ShareBasketBetweenStores
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsShareBasket"];

                return !string.IsNullOrEmpty(value) && value.ToLowerInvariant() == "true" ? true : false;
            }
        }

        public static bool VirtualContent
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsVirtualContent"];

                return !string.IsNullOrEmpty(value) && value.ToLowerInvariant() == "true" ? true : false;
            }
        }
    }
}
