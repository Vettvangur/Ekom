using Ekom.Cache;
using Ekom.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Ekom.Umb;

internal sealed class RichTextSavingNotificationHandler(
    IDataTypeService dataTypes,
    IBaseCache<IStore> stores) : INotificationAsyncHandler<ContentSavingNotification>
{
    public async Task HandleAsync(ContentSavingNotification notification, CancellationToken cancellationToken)
    {
        if (notification.Cancel) return;

        var storeAliases = stores.Cache.Values.Select(store => store.Alias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias)).Distinct(StringComparer.Ordinal).ToArray();
        if (storeAliases.Length == 0) return;

        foreach (var content in notification.SavedEntities)
        {
            foreach (var property in content.Properties)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (property.PropertyType.PropertyEditorAlias != "Ekom.Property") continue;

#if UMBRACO_18
                var dataType = await dataTypes.GetAsync(property.PropertyType.DataTypeKey).ConfigureAwait(false);
#else
                var dataType = dataTypes.GetDataType(property.PropertyType.DataTypeId);
#endif
                if (dataType == null || !TryGetStoreRichTextDataTypeKey(dataType, out var wrappedKey)) continue;
                var wrapped = await dataTypes.GetAsync(wrappedKey).ConfigureAwait(false);
                if (wrapped?.EditorAlias is not ("Umbraco.RichText" or "Umbraco.TinyMCE")) continue;

                foreach (var value in property.Values.ToArray())
                {
                    var normalized = Wrap(value.EditedValue?.ToString(), dataType.Key, storeAliases);
                    if (normalized != null)
                    {
                        // Persist through the current save, without another save or publish call.
                        content.SetValue(property.Alias, normalized, value.Culture, value.Segment);
                    }
                }
            }
        }
    }

    private static bool TryGetStoreRichTextDataTypeKey(IDataType dataType, out Guid key)
    {
        key = Guid.Empty;
        var config = dataType.ConfigurationData;
        if (config == null) return false;
        if (config.TryGetValue("useLanguages", out var useLanguages)
            && (!bool.TryParse(useLanguages?.ToString(), out var languages) || languages)) return false;
        if (!config.TryGetValue("dataType", out var wrapped)) return false;

        var element = JsonSerializer.SerializeToElement(wrapped);
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("guid", out var guid))
            element = guid;
        return element.ValueKind == JsonValueKind.String && Guid.TryParse(element.GetString(), out key);
    }

    internal static string? Wrap(string? raw, Guid dataTypeKey, IReadOnlyList<string> storeAliases)
    {
        if (raw == null || storeAliases.Count == 0) return null;
        JsonObject richText;
        var trimmed = raw.AsSpan().TrimStart();
        if (trimmed.StartsWith("{"))
        {
            try
            {
                if (JsonNode.Parse(raw) is not JsonObject obj || obj.ContainsKey("values")
                    || obj["markup"] is not JsonValue markup || !markup.TryGetValue<string>(out _)) return null;
                richText = obj;
            }
            catch (JsonException)
            {
                return null;
            }
        }
        else
        {
            // Only accept legacy HTML (or an empty value), not unrelated JSON shapes.
            if (!trimmed.IsEmpty && !trimmed.StartsWith("<")) return null;
            richText = new JsonObject { ["markup"] = raw };
        }

        var values = new JsonObject();
        foreach (var alias in storeAliases)
            values[alias] = richText.DeepClone();
        return new JsonObject
        {
            ["dtdGuid"] = dataTypeKey,
            ["type"] = "Store",
            ["values"] = values,
        }.ToJsonString();
    }
}
