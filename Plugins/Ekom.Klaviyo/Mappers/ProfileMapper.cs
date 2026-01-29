using Ekom.Klaviyo.Models;
using Ekom.Models;
using System.Text.Json.Nodes;
using Umbraco.Extensions;

namespace Ekom.Klaviyo.Mappers;

public static class ProfileMapper
{
    public static KlaviyoProfile ToKlaviyoProfile(this IOrderInfo order, KlaviyoOptions opt)
    {
        var profile = new KlaviyoProfile()
        {
            Email = order.CustomerInformation.Customer.Email,
            PhoneNumber = order.CustomerInformation.Customer.Phone,
            ExternalId = ToProfileExternalId(order, opt),
            FirstName = order.CustomerInformation.Customer.FirstName,
            LastName = order.CustomerInformation.Customer.LastName,
            Address = order.CustomerInformation.Customer.Address,
            Address2 = order.CustomerInformation.Customer.Apartment,
            ZipCode = order.CustomerInformation.Customer.ZipCode,
            City = order.CustomerInformation.Customer.City,
            Country = order.CustomerInformation.Customer.Country,
            Organisation = order.CustomerInformation.Customer.Company
        };

        return profile;
    }

    public static string ToProfileExternalId(IOrderInfo order, KlaviyoOptions opt)
    {

        if (opt.ProfileExternalIdProperty.InvariantEquals("email"))
        {
            return order.CustomerInformation.Customer.Email!;
        } else  if (opt.ProfileExternalIdProperty.InvariantEquals("phone"))
        {
            return order.CustomerInformation.Customer.Phone!;
        } else if (opt.ProfileExternalIdProperty.InvariantEquals("username"))
        {
            return order.CustomerInformation.Customer.UserName!;
        } else
        {
            var customValue = order.CustomerInformation.Customer.Properties.GetValue(opt.ProfileExternalIdProperty);

            if (!string.IsNullOrEmpty(customValue))
            {
                return customValue;
            }
        }

        throw new InvalidOperationException(
            "Klaviyo profile requires at least one identifier (email, phone_number, or external_id).");
    }


    internal static JsonObject ToProfileAttributes(this KlaviyoProfile c)
    {
        var attributes = new JsonObject();

        if (!string.IsNullOrWhiteSpace(c.Email))
            attributes["email"] = c.Email;

        if (!string.IsNullOrWhiteSpace(c.PhoneNumber))
            attributes["phone_number"] = c.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(c.ExternalId))
            attributes["external_id"] = c.ExternalId;

        if (!string.IsNullOrWhiteSpace(c.FirstName))
            attributes["first_name"] = c.FirstName;

        if (!string.IsNullOrWhiteSpace(c.LastName))
            attributes["last_name"] = c.LastName;

        if (!string.IsNullOrWhiteSpace(c.Organisation))
            attributes["organization"] = c.Organisation;

        var location = new JsonObject();

        if (!string.IsNullOrWhiteSpace(c.Address))
            location["address1"] = c.Address;

        if (!string.IsNullOrWhiteSpace(c.Address2))
            location["address2"] = c.Address2;

        if (!string.IsNullOrWhiteSpace(c.ZipCode))
            location["zip"] = c.ZipCode;

        if (!string.IsNullOrWhiteSpace(c.City))
            location["city"] = c.City;

        if (!string.IsNullOrWhiteSpace(c.Country))
            location["country"] = c.Country;

        if (location.Count > 0)
            attributes["location"] = location;

        if (c.CustomProperties is not null && c.CustomProperties.Count > 0)
        {
            var properties = new JsonObject();

            foreach (var kvp in c.CustomProperties)
            {
                if (kvp.Value is null)
                    continue;

                if (kvp.Value is string s && string.IsNullOrWhiteSpace(s))
                    continue;

                properties[kvp.Key] = JsonValue.Create(kvp.Value);
            }

            if (properties.Count > 0)
                attributes["properties"] = properties;
        }

        return attributes;
    }


    internal static JsonObject ToProfileData(this KlaviyoProfile c)
    {
        if (!c.HasIdentifier)
            throw new InvalidOperationException(
                "Klaviyo profile requires at least one identifier (email, phone_number, or external_id).");

        return new JsonObject
        {
            ["type"] = "profile",
            ["attributes"] = c.ToProfileAttributes()
        };
    }
}
