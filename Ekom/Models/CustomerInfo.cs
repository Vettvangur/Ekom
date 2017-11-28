using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models
{
    public class CustomerInfo
    {
        public string CustomerIpAddress = string.Empty;
        public Customer Customer = new Customer();
        public CustomerShippingInfo Shipping = new CustomerShippingInfo();
    }
}
