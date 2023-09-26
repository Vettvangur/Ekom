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
                if (AreShippingDetailsEmpty())
                    return true;

                return AreShippingAndCustomerDetailsSame();
            }
        }
        private bool AreShippingDetailsEmpty()
        {
            return string.IsNullOrEmpty(Shipping.Name)
                && string.IsNullOrEmpty(Shipping.Address)
                && string.IsNullOrEmpty(Shipping.City)
                && string.IsNullOrEmpty(Shipping.ZipCode);
        }

        private bool AreShippingAndCustomerDetailsSame()
        {
            return Customer.Name == Shipping.Name
                && Customer.Address == Shipping.Address
                && Customer.City == Shipping.City
                && Customer.ZipCode == Shipping.ZipCode;
        }
    }
}
