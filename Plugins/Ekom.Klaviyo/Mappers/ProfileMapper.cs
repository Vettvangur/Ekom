using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models;
using Ekom.Models;
using System.Reflection.Emit;
using System.Text.Json.Nodes;
using Umbraco.Extensions;

namespace Ekom.Klaviyo.Mappers;

internal static class ProfileMapper
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
            ZipCode = order.CustomerInformation.Customer.ZipCode,
            City = order.CustomerInformation.Customer.City,
            Country = order.CustomerInformation.Customer.Country,
            Company = order.CustomerInformation.Customer.Company
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


    public static JsonObject ToProfileAttributes(this KlaviyoProfile c)
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

        if (!string.IsNullOrWhiteSpace(c.Address))
            attributes["address"] = c.Address;

        if (!string.IsNullOrWhiteSpace(c.ZipCode))
            attributes["zip_code"] = c.ZipCode;

        if (!string.IsNullOrWhiteSpace(c.City))
            attributes["city"] = c.City;

        if (!string.IsNullOrWhiteSpace(c.Country))
            attributes["country"] = c.Country;

        if (!string.IsNullOrWhiteSpace(c.Company))
            attributes["company"] = c.Company;

        if (c.CustomProperties is not null && c.CustomProperties.Count > 0)
        {
            CustomPropertiesMerger.MergeCustomProperties(attributes, c.CustomProperties);
        }

        return attributes;
    }

    public static JsonObject ToProfileData(this KlaviyoProfile c)
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
