using Ekom.Klaviyo.Models.Orders;
using Ekom.Klaviyo.Models.Profiles;
using Ekom.Models;
using System.Text.Json.Nodes;
using Umbraco.Extensions;

namespace Ekom.Klaviyo.Mappers;

public static class ProfileMapper
{
    public static KlaviyoOrderProfile ToKlaviyoProfile(this IOrderInfo order, KlaviyoOptions opt)
    {
        return new KlaviyoOrderProfile()
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
    }

    public static string ToProfileExternalId(IOrderInfo order, KlaviyoOptions opt)
    {
        if (opt.ProfileExternalIdProperty.InvariantEquals("email"))
            return order.CustomerInformation.Customer.Email!;

        if (opt.ProfileExternalIdProperty.InvariantEquals("phone"))
            return order.CustomerInformation.Customer.Phone!;

        if (opt.ProfileExternalIdProperty.InvariantEquals("username"))
            return order.CustomerInformation.Customer.UserName!;

        var customValue = order.CustomerInformation.Customer.Properties.GetValue(opt.ProfileExternalIdProperty);
        if (!string.IsNullOrEmpty(customValue))
            return customValue;

        throw new InvalidOperationException(
            "Klaviyo profile requires at least one identifier (email, phone_number, or external_id).");
    }

    public static KlaviyoProfileUpdate ToKlaviyoProfileUpsert(this IOrderInfo order, string storeAlias, KlaviyoOptions opt)
    {
        var customer = order.ToKlaviyoCustomer(opt);

        var attributes = new KlaviyoProfileAttributes
        {
            FirstName = order.CustomerInformation.Customer.FirstName,
            LastName = order.CustomerInformation.Customer.LastName,
            Address = order.CustomerInformation.Customer.Address,
            Address2 = order.CustomerInformation.Customer.Apartment,
            ZipCode = order.CustomerInformation.Customer.ZipCode,
            City = order.CustomerInformation.Customer.City,
            Country = order.CustomerInformation.Customer.Country,
            Organisation = order.CustomerInformation.Customer.Company
        };

        return new KlaviyoProfileUpdate(
            StoreAlias: storeAlias,
            Profile: new KlaviyoProfile
            {
                Customer = customer,
                Attributes = attributes
            });
    }

    public static KlaviyoCustomer ToKlaviyoCustomer(this IOrderInfo order, KlaviyoOptions opt)
    {
        return new KlaviyoCustomer
        {
            Email = order.CustomerInformation.Customer.Email,
            PhoneNumber = order.CustomerInformation.Customer.Phone,
            ExternalId = ToProfileExternalId(order, opt)
        };
    }

    public static KlaviyoProfile ToProfile(this KlaviyoOrderProfile o)
            => new()
            {
                Customer = new KlaviyoCustomer
                {
                    Email = o.Email,
                    PhoneNumber = o.PhoneNumber,
                    ExternalId = o.ExternalId
                },
                Attributes = new KlaviyoProfileAttributes
                {
                    FirstName = o.FirstName,
                    LastName = o.LastName,
                    Address = o.Address,
                    Address2 = o.Address2,
                    ZipCode = o.ZipCode,
                    City = o.City,
                    Country = o.Country,
                    Organisation = o.Organisation,
                    CustomProperties = o.CustomProperties
                }
            };

    // -----------------------------
    // JSON payload builders (Klaviyo expects snake_case keys)
    // -----------------------------

    // 1) /api/profile-import
    public static object ToProfileImportRequest(this KlaviyoProfileUpdate u)
    {
        return new JsonObject
        {
            ["data"] = u.Profile.ToProfileData()
        };
    }

    // 1b) /api/lists/{id}/relationships/profiles
    public static object ToAddToListRequest(this KlaviyoProfile p)
    {
        if (!p.Customer.HasIdentifier)
            throw new InvalidOperationException(
                "Klaviyo profile requires at least one identifier (email, phone_number, external_id, or klaviyo profile id).");

        return new JsonObject
        {
            ["data"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "profile",
                    ["attributes"] = p.ToProfileAttributes()
                })
        };
    }

    // 2) /api/profile-subscription-bulk-create-jobs
    public static object ToBulkSubscribeJobRequest(this KlaviyoProfileConsentRequest u)
    {
        return u.ToSubscriptionJobRequest(
            jobType: "profile-subscription-bulk-create-job",
            includeSubscriptionsObject: true);
    }

    // 3) /api/profile-subscription-bulk-delete-jobs
    public static object ToBulkUnsubscribeJobRequest(this KlaviyoProfileConsentRequest u)
    {
        return u.ToSubscriptionJobRequest(
            jobType: "profile-subscription-bulk-delete-job",
            includeSubscriptionsObject: false);
    }

  

    private static object ToSubscriptionJobRequest(this KlaviyoProfileConsentRequest u, string jobType, bool includeSubscriptionsObject)
    {
        var profileAttributes = new JsonObject();

        profileAttributes["email"] = u.Email;


        if (includeSubscriptionsObject)
        {
            var subscriptions = new JsonObject();

            foreach (var c in u.Consents)
            {
                var channelKey = c.Channel switch
                {
                    KlaviyoProfileConsentChannel.Email => "email",
                    KlaviyoProfileConsentChannel.Sms => "sms",
                    KlaviyoProfileConsentChannel.Push => null,
                    _ => null
                };

                if (channelKey is null)
                    continue;

                var consentValue = c.State == KlaviyoProfileConsentState.Subscribed
                    ? "SUBSCRIBED"
                    : "UNSUBSCRIBED";

                subscriptions[channelKey] = new JsonObject
                {
                    ["marketing"] = new JsonObject
                    {
                        ["consent"] = consentValue
                    }
                };
            }

            if (subscriptions.Count > 0)
                profileAttributes["subscriptions"] = subscriptions;
        }

        var profiles = new JsonObject
        {
            ["data"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "profile",
                    ["attributes"] = profileAttributes
                })
        };

        var attributes = new JsonObject
        {
            ["profiles"] = profiles
        };

        var data = new JsonObject
        {
            ["type"] = jobType,
            ["attributes"] = attributes
        };

        return new JsonObject
        {
            ["data"] = data
        };
    }


// -----------------------------
// Shared profile JSON (profile-import shape)
// -----------------------------
internal static JsonObject ToProfileAttributes(this KlaviyoProfile p)
    {
        var attributes = new JsonObject();
        var c = p.Customer;

        if (!string.IsNullOrWhiteSpace(c.Email))
            attributes["email"] = c.Email;

        if (!string.IsNullOrWhiteSpace(c.PhoneNumber))
            attributes["phone_number"] = c.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(c.ExternalId))
            attributes["external_id"] = c.ExternalId;

        if (p.Attributes is null)
            return attributes;

        var a = p.Attributes;
        var (firstName, lastName) = GetNameParts(a);

        if (!string.IsNullOrWhiteSpace(firstName))
            attributes["first_name"] = firstName;

        if (!string.IsNullOrWhiteSpace(lastName))
            attributes["last_name"] = lastName;

        if (!string.IsNullOrWhiteSpace(a.Organisation))
            attributes["organization"] = a.Organisation;

        var location = new JsonObject();

        if (!string.IsNullOrWhiteSpace(a.Address))
            location["address1"] = a.Address;

        if (!string.IsNullOrWhiteSpace(a.Address2))
            location["address2"] = a.Address2;

        if (!string.IsNullOrWhiteSpace(a.ZipCode))
            location["zip"] = a.ZipCode;

        if (!string.IsNullOrWhiteSpace(a.City))
            location["city"] = a.City;

        if (!string.IsNullOrWhiteSpace(a.Country))
            location["country"] = a.Country;

        if (location.Count > 0)
            attributes["location"] = location;

        if (a.CustomProperties is not null && a.CustomProperties.Count > 0)
        {
            var properties = new JsonObject();

            foreach (var kvp in a.CustomProperties)
            {
                if (kvp.Value is null) continue;
                if (kvp.Value is string s && string.IsNullOrWhiteSpace(s)) continue;

                properties[kvp.Key] = JsonValue.Create(kvp.Value);
            }

            if (properties.Count > 0)
                attributes["properties"] = properties;
        }

        return attributes;
    }

    private static (string? FirstName, string? LastName) GetNameParts(KlaviyoProfileAttributes attributes)
    {
        if (!string.IsNullOrWhiteSpace(attributes.FirstName) || !string.IsNullOrWhiteSpace(attributes.LastName))
            return (attributes.FirstName, attributes.LastName);

        if (string.IsNullOrWhiteSpace(attributes.FullName))
            return (null, null);

        var parts = attributes.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return (null, null);

        if (parts.Length == 1)
            return (parts[0], null);

        var lastName = string.Join(' ', parts, 1, parts.Length - 1);
        return (parts[0], lastName);
    }

    internal static JsonObject ToProfileData(this KlaviyoProfile p)
    {
        if (!p.Customer.HasIdentifier)
            throw new InvalidOperationException(
                "Klaviyo profile requires at least one identifier (email, phone_number, external_id, or klaviyo profile id).");

        return new JsonObject
        {
            ["type"] = "profile",
            ["attributes"] = p.ToProfileAttributes()
        };
    }

    internal static JsonObject ToProfileAttributes(this KlaviyoOrderProfile c)
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
                if (kvp.Value is null) continue;
                if (kvp.Value is string s && string.IsNullOrWhiteSpace(s)) continue;

                properties[kvp.Key] = JsonValue.Create(kvp.Value);
            }

            if (properties.Count > 0)
                attributes["properties"] = properties;
        }

        return attributes;
    }

}
