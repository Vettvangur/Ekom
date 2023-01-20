namespace Ekom.Models
{
    public class CustomerInfo
    {
        public string CustomerIpAddress = string.Empty;
        public Customer Customer = new Customer();
        public CustomerShippingInfo Shipping = new CustomerShippingInfo();
    }
}
