using Ekom.Exceptions;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Encodings.Web;

namespace Ekom.Utilities;

internal static class OrderCustomData
{
    internal static JObject ParseJson(string json)
    {
        try
        {
            return JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        }
        catch (JsonException)
        {
            throw new OrderCustomDataException("Invalid JSON or duplicate properties in the update request.");
        }
    }

    private const string Prefix = "CustomData[";

    internal static bool IsField(string name) => name.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    internal static Dictionary<string, string> ReadFields(IEnumerable<KeyValuePair<string, string>>? fields)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in fields ?? [])
        {
            if (!IsField(pair.Key)) continue;
            if (!pair.Key.EndsWith(']')) throw new OrderCustomDataException("Invalid CustomData field name.");
            Add(result, pair.Key[Prefix.Length..^1], pair.Value);
        }
        return result;
    }

    internal static void Add(Dictionary<string, string> target, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains('[', StringComparison.Ordinal) || key.Contains(']', StringComparison.Ordinal) || value == null)
            throw new OrderCustomDataException("CustomData requires nonblank keys without brackets and non-null string values.");
        if (!target.TryAdd(key, value))
            throw new OrderCustomDataException("CustomData keys must be unique ignoring case.");
    }

    internal static void Merge(Dictionary<string, string> target, IReadOnlyDictionary<string, string> updates)
    {
        foreach (var pair in updates) target[pair.Key] = pair.Value;
    }

    internal static Dictionary<string, string> FromForm(IFormCollection form, bool encodeOtherFields = true)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in form)
        {
            if (IsField(pair.Key) && pair.Value.Count != 1)
                throw new OrderCustomDataException("CustomData fields must have exactly one value.");
            if (!result.TryAdd(pair.Key, Encode(pair.Key, pair.Value.ToString(), encodeOtherFields)))
                throw new OrderCustomDataException("Request keys must be unique ignoring case.");
        }
        ReadFields(result);
        return result;
    }

    internal static Dictionary<string, string> FromJson(JObject json, bool encodeOtherFields = true)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var customData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hasCustomData = false;
        foreach (var property in json.Properties())
        {
            if (property.Name.Equals("CustomData", StringComparison.OrdinalIgnoreCase))
            {
                if (hasCustomData || property.Value is not JObject data)
                    throw new OrderCustomDataException("CustomData must be a single object of string values.");
                hasCustomData = true;
                foreach (var entry in data.Properties())
                {
                    if (entry.Value.Type != JTokenType.String)
                        throw new OrderCustomDataException("CustomData values must be strings.");
                    Add(customData, entry.Name, entry.Value.Value<string>()!);
                }
            }
            else
            {
                if (IsField(property.Name) && property.Value.Type != JTokenType.String)
                    throw new OrderCustomDataException("CustomData values must be strings.");
                if (!result.TryAdd(property.Name, Encode(property.Name, property.Value.ToString(), encodeOtherFields)))
                    throw new OrderCustomDataException("Request keys must be unique ignoring case.");
            }
        }
        foreach (var pair in ReadFields(result)) Add(customData, pair.Key, pair.Value);
        foreach (var pair in customData) result[Prefix + pair.Key + "]"] = pair.Value;
        return result;
    }

    private static string Encode(string key, string value, bool encodeOtherFields)
        => encodeOtherFields && !IsField(key) ? HtmlEncoder.Default.Encode(value) : value;
}
