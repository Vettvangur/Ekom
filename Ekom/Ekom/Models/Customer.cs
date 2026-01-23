using Ekom.Utilities;

namespace Ekom.Models;

public class Customer
{
    public string Name
    {
        get
        {
            var fullName = Properties.GetValue("customerName");

            if (!string.IsNullOrWhiteSpace(fullName))
                return fullName.Trim();

            return $"{FirstName} {LastName}".Trim();
        }
    }

    public string FirstName
    {
        get => Properties.GetValue("customerFirstName")?.Trim() ?? "";
    }

    public string LastName
    {
        get
        {
            var lastName = Properties.GetValue("customerLastName");

            if (!string.IsNullOrWhiteSpace(lastName))
                return lastName.Trim();

            // Fallback: derive from Name
            var name = Name;
            if (string.IsNullOrWhiteSpace(name))
                return "";

            var parts = name
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
    public string Region
    {
        get
        {
            return Properties.GetValue("customerRegion");
        }
    }
    public string Company
    {
        get
        {
            return Properties.GetValue("customerCompany");
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
    public string Value(string alias)
    {
        {
            return Properties.GetValue(alias);
        }
    }
}
