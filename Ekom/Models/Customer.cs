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
                return Properties.GetProperty("customerName");
            }
        }
        public string Email {
            get
            {
                return Properties.GetProperty("customerEmail");
            }
        }
        public string Address {
            get
            {
                return Properties.GetProperty("customerAddress");
            }
        }
        public string City {
            get
            {
                return Properties.GetProperty("customerCity");
            }
        }
        public string Country {
            get
            {
                return Properties.GetProperty("customerCountry");
            }
        }
        public string ZipCode {
            get
            {
                return Properties.GetProperty("customerZipCode");
            }
        }
        public string Phone {
            get
            {
                return Properties.GetProperty("customerPhone");
            }
        }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
