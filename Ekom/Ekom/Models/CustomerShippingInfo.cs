using Ekom.Utilities;

namespace Ekom.Models;

public class CustomerShippingInfo
{
    public string Name
    {
        get
        {
            var fullName = Value("shippingName");

            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName.Trim();

            return $"{FirstName} {LastName}".Trim();
        }
    }

    public string FirstName
    {
        get
        {
            var firstName = Value("shippingFirstName");

            if (!string.IsNullOrWhiteSpace(firstName))
            {
                return firstName.Trim();
            }

            // Fallback: derive from Name
            var fullName = Value("shippingName");
            if (string.IsNullOrWhiteSpace(fullName))
                return "";

            var parts = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length > 0 ? parts[0] : "";

        }
    }

    public string LastName
    {
        get
        {
            var lastName = Value("shippingLastName");

            if (!string.IsNullOrWhiteSpace(lastName))
                return lastName.Trim();

            // Fallback: derive from Name
            var fullName = Value("shippingName");
            if (string.IsNullOrWhiteSpace(fullName))
                return "";

            var parts = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Single-word name → no last name
            if (parts.Length < 2)
                return "";

            // Everything except first word is treated as last name
            return string.Join(' ', parts.Skip(1));
        }
    }
    public string Email
    {

        get
        {
            return Value("shippingEmail");
        }
    }
    public string Phone
    {

        get
        {
            return Value("shippingPhone");
        }
    }
    public string Address
    {
        get
        {
            return Value("shippingAddress");
        }
    }
    public string City
    {
        get
        {
            return Value("shippingCity");
        }
    }
    public string Apartment
    {
        get
        {
            return Value("shippingApartment");
        }
    }
    public string Country
    {
        get
        {
            return Value("shippingCountry");
        }
    }
    public string ZipCode
    {
        get
        {
            return Value("shippingZipCode");
        }
    }

    public string Region
    {
        get
        {
            return Value("shippingRegion");
        }
    }


    public Dictionary<string, string> Properties = new Dictionary<string, string>();

    public string Value(string alias)
    {
        {
            return Properties.GetValue(alias);
        }
    }
}
