using Ekom.Algolia.Models.Indexing;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Algolia.Indexing;

internal static class AlgoliaContentRecordSizeInspector
{
    private const int ReportedFieldCount = 5;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static int GetSizeBytes(AlgoliaContentRecord record)
        => JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions).Length;

    public static AlgoliaContentRecordSizeInfo Inspect(AlgoliaContentRecord record)
    {
        var sizeBytes = GetSizeBytes(record);
        var fieldSizes = GetFields(record)
            .Select(field => new AlgoliaContentFieldSize(field.Key, GetFieldSize(field.Key, field.Value)))
            .OrderByDescending(field => field.SizeBytes)
            .ThenBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Take(ReportedFieldCount)
            .ToList();

        return new AlgoliaContentRecordSizeInfo(record, sizeBytes, fieldSizes);
    }

    public static bool TryGetObjectId(string message, out string? objectId)
    {
        objectId = null;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        try
        {
            using var document = JsonDocument.Parse(message);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            if (document.RootElement.TryGetProperty("objectID", out var objectIdElement) && objectIdElement.ValueKind == JsonValueKind.String)
            {
                objectId = objectIdElement.GetString();
                return !string.IsNullOrWhiteSpace(objectId);
            }

            if (!document.RootElement.TryGetProperty("message", out var messageElement) || messageElement.ValueKind != JsonValueKind.String)
                return false;

            objectId = ExtractObjectId(messageElement.GetString());
            return !string.IsNullOrWhiteSpace(objectId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ExtractObjectId(string? message)
    {
        const string marker = "objectID=";
        const string suffix = " is too big";

        if (string.IsNullOrWhiteSpace(message))
            return null;

        var startIndex = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
            return null;

        startIndex += marker.Length;
        var endIndex = message.IndexOf(suffix, startIndex, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0)
            endIndex = message.Length;

        var value = message[startIndex..endIndex].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static IEnumerable<KeyValuePair<string, object?>> GetFields(AlgoliaContentRecord record)
    {
        yield return new("objectID", record.ObjectID);
        yield return new("nodeId", record.NodeId);
        yield return new("contentTypeAlias", record.ContentTypeAlias);
        yield return new("url", record.Url);
        yield return new("name", record.Name);
        yield return new("updateDate", record.UpdateDate);
        yield return new("updateDateUnixSecond", record.UpdateDateUnixSecond);
        yield return new("createDate", record.CreateDate);
        yield return new("createDateUnixSecond", record.CreateDateUnixSecond);

        foreach (var field in record.Data)
            yield return new(field.Key, field.Value);
    }

    private static int GetFieldSize(string fieldName, object? value)
    {
        var nameSize = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(fieldName, JsonOptions));
        var valueSize = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions).Length;
        return nameSize + valueSize + 1;
    }
}

internal sealed record AlgoliaContentRecordSizeInfo(
    AlgoliaContentRecord Record,
    int SizeBytes,
    IReadOnlyList<AlgoliaContentFieldSize> LargestFields);

internal sealed record AlgoliaContentFieldSize(string Name, int SizeBytes);
