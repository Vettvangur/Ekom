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
                return Properties.GetValue("shippingName");
            }
        }
        public string FirstName
        {

            get
            {
                return Properties.GetValue("shippingFirstName");
            }
        }
        public string LastName
        {

            get
            {
                return Properties.GetValue("shippingLastName");
            }
        }
        public string Address
        {
            get
            {
                return Properties.GetValue("shippingAddress");
            }
        }
        public string City
        {
            get
            {
                return Properties.GetValue("shippingCity");
            }
        }
        public string Apartment
        {
            get
            {
                return Properties.GetValue("shippingApartment");
            }
        }
        public string Country
        {
            get
            {
                return Properties.GetValue("shippingCountry");
            }
        }
        public string ZipCode
        {
            get
            {
                return Properties.GetValue("shippingZipCode");
            }
        }


        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
