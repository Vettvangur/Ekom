using Ekom.Utilities;

namespace Ekom.Models
{
    public class Customer
    {
        public string Name
        {

            get
            {
                return (string.IsNullOrEmpty(Properties.GetValue("customerName")) ? (FirstName + " " + LastName) : Properties.GetValue("customerName"))?.Trim();
            }
        }
        public string FirstName
        {

            get
            {
                return Properties.GetValue("customerFirstName");
            }
        }
        public string LastName
        {

            get
            {
                return Properties.GetValue("customerLastName");
            }
        }
        public string Email
        {
            get
            {
                return Properties.GetValue("customerEmail");
            }
        }
        public string Address
        {
            get
            {
                return Properties.GetValue("customerAddress");
            }
        }
        public string City
        {
            get
            {
                return Properties.GetValue("customerCity");
            }
        }
        public string Apartment
        {
            get
            {
                return Properties.GetValue("customerApartment");
            }
        }
        public string Country
        {
            get
            {
                return Properties.GetValue("customerCountry");
            }
        }
        public string ZipCode
        {
            get
            {
                return Properties.GetValue("customerZipCode");
            }
        }
        public string Phone
        {
            get
            {
                return Properties.GetValue("customerPhone");
            }
        }
        public int UserId { get; set; }
        public string UserName { get; set; }
        public Dictionary<string, string> Properties = new Dictionary<string, string>();
    }
}
