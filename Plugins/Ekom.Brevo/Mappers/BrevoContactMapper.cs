using Ekom.Brevo.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Brevo.Mappers;

internal static class BrevoContactMapper
{
    public static JsonObject Attributes(BrevoContact contact)
    {
        BrevoConfiguration.ValidateEmail(contact.Email);
        var attributes = new JsonObject();
        foreach (var (key, value) in contact.Attributes)
        {
            // EMAIL can change identity and clear an existing blocklist in Brevo.
            if (string.IsNullOrWhiteSpace(key) || key != key.ToUpperInvariant()
                || key is "EMAIL" or "EXT_ID")
                throw new ArgumentException("Custom attributes must be uppercase and cannot replace EMAIL or EXT_ID.", nameof(contact));
            attributes[key] = JsonSerializer.SerializeToNode(value);
        }
        if (contact.FirstName != null) attributes["FIRSTNAME"] = contact.FirstName;
        if (contact.LastName != null) attributes["LASTNAME"] = contact.LastName;
        if (contact.Phone != null) attributes["SMS"] = contact.Phone;
        return attributes;
    }

    public static JsonObject Upsert(BrevoContact contact)
    {
        var payload = new JsonObject
        {
            ["email"] = contact.Email,
            ["attributes"] = Attributes(contact),
            ["updateEnabled"] = true,
        };
        if (!string.IsNullOrWhiteSpace(contact.ExternalId)) payload["ext_id"] = contact.ExternalId;
        return payload;
    }
}
