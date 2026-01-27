using Ekom.Utilities;

namespace Ekom.Models;

public class Customer
{
    public string Name
    {
        get
        {
            var fullName = Value("customerName");

            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName.Trim();

            return $"{FirstName} {LastName}".Trim();
        }
    }

    public string FirstName
    {
        get => Value("customerFirstName")?.Trim() ?? "";
    }

    public string LastName
    {
        get
        {
            var lastName = Value("customerLastName");

            if (!string.IsNullOrWhiteSpace(lastName))
                return lastName.Trim();

            // Fallback: derive from Name
            var fullName = Value("customerName");
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
            return Value("customerEmail");
        }
    }
    public string Address
    {
        get
        {
            return Value("customerAddress");
        }
    }
    public string City
    {
        get
        {
            return Value("customerCity");
        }
    }
    public string Apartment
    {
        get
        {
            return Value("customerApartment");
        }
    }
    public string Country
    {
        get
        {
            return Value("customerCountry");
        }
    }
    public string Region
    {
        get
        {
            return Value("customerRegion");
        }
    }
    public string State
    {
        get
        {
            return Value("customerState");
        }
    }
    public string Company
    {
        get
        {
            return Value("customerCompany");
        }
    }
    public string ZipCode
    {
        get
        {
            return Value("customerZipCode");
        }
    }
    public string Phone
    {
        get
        {
            return Value("customerPhone");
        }
    }
    public int UserId { get; set; }
    public string UserName { get; set; }
    public Dictionary<string, string> Properties = new Dictionary<string, string>();
    public string Value(string alias)
    {
        {
            return Properties.GetValue(alias);
        }
    }
}
