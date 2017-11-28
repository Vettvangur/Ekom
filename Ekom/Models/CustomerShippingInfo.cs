using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class CustomerShippingInfo
    {
        public string Name
        {

            get
            {
                return Properties.GetProperty("shippingName");
            }
        }
        public string Address
        {
            get
            {
                return Properties.GetProperty("shippingAddress");
            }
        }
        public string City
        {
            get
            {
                return Properties.GetProperty("shippingCity");
            }
        }
        public string Country
        {
            get
            {
                return Properties.GetProperty("shippingCountry");
            }
        }
        public string ZipCode
        {
            get
            {
                return Properties.GetProperty("shippingZipCode");
            }
        }


        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
