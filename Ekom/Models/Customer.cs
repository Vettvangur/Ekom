using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ekom.Utilities;

namespace Ekom.Models
{
    public class Customer
    {
        public string Name {

            get
            {
                return Properties.GetPropertyValue("customerName");
            }
        }
        public string Email {
            get
            {
                return Properties.GetPropertyValue("customerEmail");
            }
        }
        public string Address {
            get
            {
                return Properties.GetPropertyValue("customerAddress");
            }
        }
        public string City {
            get
            {
                return Properties.GetPropertyValue("customerCity");
            }
        }
        public string Country {
            get
            {
                return Properties.GetPropertyValue("customerCountry");
            }
        }
        public string ZipCode {
            get
            {
                return Properties.GetPropertyValue("customerZipCode");
            }
        }
        public string Phone {
            get
            {
                return Properties.GetPropertyValue("customerPhone");
            }
        }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
