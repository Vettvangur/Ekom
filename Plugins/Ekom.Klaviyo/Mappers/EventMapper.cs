using Ekom.Klaviyo.Models.Events;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Klaviyo.Mappers;

internal static class EventMapper
{
    internal static object ToCustomEventRequest(this KlaviyoCustomEvent e, KlaviyoOptions opt)
    {
        var occurredAt = e.OccurredAt == default
            ? DateTimeOffset.UtcNow
            : e.OccurredAt;

        var metricName = e.EventName + (opt.Testing ? " Test" : "");
        var uniqueId = BuildUniqueId(e.StoreAlias, e.EventName, e.UniqueId, opt.Testing);

        return new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["type"] = "event",
                ["attributes"] = new JsonObject
                {
                    ["unique_id"] = uniqueId,
                    ["metric"] = new JsonObject
                    {
                        ["data"] = new JsonObject
                        {
                            ["type"] = "metric",
                            ["attributes"] = new JsonObject
                            {
                                ["name"] = metricName
                            }
                        }
                    },
                    ["profile"] = new JsonObject
                    {
                        ["data"] = new JsonObject
                        {
                            ["type"] = "profile",
                            ["attributes"] = e.Profile.ToProfileAttributes()
                        }
                    },
                    ["time"] = occurredAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                    ["properties"] = ToPropertiesObject(e.Properties)
                }
            }
        };
    }

    private static JsonObject ToPropertiesObject(object? properties)
    {
        if (properties is null)
            return [];

        if (properties is JsonObject jsonObject)
            return jsonObject.DeepClone().AsObject();

        var node = JsonSerializer.SerializeToNode(properties);
        return node is JsonObject serializedObject
            ? serializedObject
            : [];
    }

    private static string BuildUniqueId(string storeAlias, string eventName, string? uniqueId, bool testing)
    {
        var id = string.IsNullOrWhiteSpace(uniqueId)
            ? Guid.NewGuid().ToString("N")
            : uniqueId;

        var suffix = testing ? ":Test" : string.Empty;

        return $"{storeAlias}:{eventName}:{id}{suffix}";
    }

    private static JsonObject ToProfileAttributes(this KlaviyoEventProfile c)
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

        if (c.CustomProperties is null || c.CustomProperties.Count == 0)
            return attributes;

        var properties = new JsonObject();

        foreach (var kvp in c.CustomProperties)
        {
            if (kvp.Value is null) continue;
            if (kvp.Value is string s && string.IsNullOrWhiteSpace(s)) continue;

            properties[kvp.Key] = JsonValue.Create(kvp.Value);
        }

        if (properties.Count > 0)
            attributes["properties"] = properties;

        return attributes;
    }
}
