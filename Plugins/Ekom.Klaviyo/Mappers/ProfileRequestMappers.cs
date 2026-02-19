using Ekom.Klaviyo.Models.Profiles;

namespace Ekom.Klaviyo.Mappers;

public static class KlaviyoProfileRequestMappers
{
    public static object ToProfileUpdateRequest(this KlaviyoProfileUpdate u, KlaviyoOptions opt)
    {
        return new
        {
            storeAlias = u.StoreAlias,
            profile = u.Profile.ToProfileObject(includeAttributes: true),
            // Optional: you can include flags from opt if useful
            // enabled = opt.Enabled
        };
    }

    public static object ToSubscriptionUpdateRequest(this KlaviyoProfileSubscriptionUpdate u, KlaviyoOptions opt)
    {
        return new
        {
            storeAlias = u.StoreAlias,
            profile = u.Profile.ToProfileObject(includeAttributes: true),
            consents = u.Consents?.Select(c => c.ToConsentObject()).ToArray()
        };
    }

    public static object ToConsentUpdateRequest(this KlaviyoProfileConsentRequest u, KlaviyoOptions opt)
    {
        return new
        {
            storeAlias = u.StoreAlias,
            profile = new Dictionary<string, object?>
            {
                ["email"] = NullIfWhiteSpace(u.Email)
            },
            consents = u.Consents.Select(c => c.ToConsentObject()).ToArray()
        };
    }

    private static object ToConsentObject(this KlaviyoProfileConsentChange c)
    {
        return new
        {
            channel = c.Channel.ToString().ToLowerInvariant(), // email/sms/push
            state = c.State.ToString().ToLowerInvariant(),     // subscribed/unsubscribed
            source = NullIfWhiteSpace(c.Source),
            timestampUtc = c.TimestampUtc,
            consentTextVersion = NullIfWhiteSpace(c.ConsentTextVersion),
            ip = NullIfWhiteSpace(c.Ip),
            userAgent = NullIfWhiteSpace(c.UserAgent)
        };
    }

    private static object ToProfileObject(this KlaviyoProfile p, bool includeAttributes)
    {
        var customer = p.Customer;

        // identity
        var profile = new Dictionary<string, object?>();

        Add(profile, "email", NullIfWhiteSpace(customer.Email));
        Add(profile, "phoneNumber", NullIfWhiteSpace(customer.PhoneNumber));
        Add(profile, "externalId", NullIfWhiteSpace(customer.ExternalId));

        if (!includeAttributes || p.Attributes is null)
            return profile;

        var a = p.Attributes;

        // standard attributes
        Add(profile, "firstName", NullIfWhiteSpace(a.FirstName));
        Add(profile, "lastName", NullIfWhiteSpace(a.LastName));
        Add(profile, "address", NullIfWhiteSpace(a.Address));
        Add(profile, "address2", NullIfWhiteSpace(a.Address2));
        Add(profile, "zipCode", NullIfWhiteSpace(a.ZipCode));
        Add(profile, "city", NullIfWhiteSpace(a.City));
        Add(profile, "country", NullIfWhiteSpace(a.Country));
        Add(profile, "organisation", NullIfWhiteSpace(a.Organisation));

        // custom props (escape hatch)
        if (a.CustomProperties is not null)
        {
            foreach (var kv in a.CustomProperties)
            {
                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                if (kv.Value is null) continue;

                // Avoid collisions with standard keys unless you explicitly want override behavior
                if (!profile.ContainsKey(kv.Key))
                    profile[kv.Key] = kv.Value;
            }
        }

        return profile;
    }

    private static string? NullIfWhiteSpace(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s;

    private static void Add(Dictionary<string, object?> dict, string key, object? value)
    {
        if (value is null) return;
        dict[key] = value;
    }
}
