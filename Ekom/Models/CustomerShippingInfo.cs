using Ekom.Utilities;
using System.Collections.Generic;

namespace Ekom.Models
{
    public class CustomerShippingInfo
    {
        public string Name
        {

            get
            {
                return Properties.GetPropertyValue("shippingName");
            }
        }
        public string FirstName
        {

            get
            {
                return Properties.GetPropertyValue("shippingFirstName");
            }
        }
        public string LastName
        {

            get
            {
                return Properties.GetPropertyValue("shippingLastName");
            }
        }
        public string Address
        {
            get
            {
                return Properties.GetPropertyValue("shippingAddress");
            }
        }
        public string City
        {
            get
            {
                return Properties.GetPropertyValue("shippingCity");
            }
        }
        public string Apartment
        {
            get
            {
                return Properties.GetPropertyValue("shippingApartment");
            }
        }
        public string Country
        {
            get
            {
                return Properties.GetPropertyValue("shippingCountry");
            }
        }
        public string ZipCode
        {
            get
            {
                return Properties.GetPropertyValue("shippingZipCode");
            }
        }


        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
