using Ekom.Utilities;
using System.Collections.Generic;

namespace Ekom.Models
{
    public class Customer
    {
        public string Name
        {

            get
            {
                return (string.IsNullOrEmpty(Properties.GetPropertyValue("customerName")) ? (FirstName + " " + LastName) : Properties.GetPropertyValue("customerName"))?.Trim();
            }
        }
        public string FirstName
        {

            get
            {
                return Properties.GetPropertyValue("customerFirstName");
            }
        }
        public string LastName
        {

            get
            {
                return Properties.GetPropertyValue("customerLastName");
            }
        }
        public string Email
        {
            get
            {
                return Properties.GetPropertyValue("customerEmail");
            }
        }
        public string Address
        {
            get
            {
                return Properties.GetPropertyValue("customerAddress");
            }
        }
        public string City
        {
            get
            {
                return Properties.GetPropertyValue("customerCity");
            }
        }
        public string Apartment
        {
            get
            {
                return Properties.GetPropertyValue("customerApartment");
            }
        }
        public string Country
        {
            get
            {
                return Properties.GetPropertyValue("customerCountry");
            }
        }
        public string ZipCode
        {
            get
            {
                return Properties.GetPropertyValue("customerZipCode");
            }
        }
        public string Phone
        {
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
