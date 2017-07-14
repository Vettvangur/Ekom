using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Utilities;

namespace uWebshop
{
    /// <summary>
    /// Controls configuration of uWebshop
    /// </summary>
    public class Configuration
    {
        public virtual string ExamineSearcher
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsExamineSearcher"];

                return value ?? "ExternalSearcher";
            }
        }

        public virtual bool ShareBasketBetweenStores
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsShareBasket"];

                return value.ConvertToBool();
            }
        }

        public virtual bool VirtualContent
        {
            get
            {
                var value = ConfigurationManager.AppSettings["uwbsVirtualContent"];

                return value.ConvertToBool();
            }
        }
    }
}
