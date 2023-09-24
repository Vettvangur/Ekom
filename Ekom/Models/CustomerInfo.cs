namespace Ekom.Models
{
    public class CustomerInfo
    {
        public string CustomerIpAddress = string.Empty;
        public Customer Customer = new Customer();
        public CustomerShippingInfo Shipping = new CustomerShippingInfo();
        public bool IsBillingSameAsShipping
        {
            get
            {
                return Customer.Name != Shipping.Name || Customer.Address != Shipping.Address || Customer.City != Shipping.City || Customer.ZipCode != Shipping.ZipCode;
            }
        }
    }
}
