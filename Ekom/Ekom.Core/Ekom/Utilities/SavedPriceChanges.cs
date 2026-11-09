using Ekom.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Utilities;

/// <summary>Consume explicit price-field edits on document save without replaying stale node prices.</summary>
public static class SavedPriceChanges
{
    public static IReadOnlyList<PriceMutation> Create(Guid key, string? value, IReadOnlyList<IStore> stores,
        string propertyAlias = "price")
    {
        if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith("{", StringComparison.Ordinal)) return [];
        using var json = JsonDocument.Parse(value);
        var edited = new List<(string Store, string Currency)>();
        foreach (var store in json.RootElement.EnumerateObject())
        {
            if (store.Value.ValueKind != JsonValueKind.Array) continue;
            foreach (var row in store.Value.EnumerateArray())
                if (row.ValueKind == JsonValueKind.Object && row.TryGetProperty("EditId", out var id)
                    && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
                {
                    if (!row.TryGetProperty("Currency", out var currency) || currency.ValueKind != JsonValueKind.String)
                        throw new ArgumentException("An edited price must specify a currency.");
                    edited.Add((store.Name, currency.GetString()!));
                }
        }
        if (edited.Count == 0) return [];
        var values = PriceHelper.ReadNodePrices(key, value, stores, propertyAlias);
        var changes = new List<PriceMutation>();
        foreach (var edit in edited)
        {
            var row = values.FirstOrDefault(row => row.StoreAlias.Equals(edit.Store, StringComparison.OrdinalIgnoreCase)
                && row.Currency.Equals(edit.Currency, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("An edited price has an unknown store, currency, or invalid amount.");
            changes.Add(row with { CheckExpected = false });
        }
        return changes;
    }

    public static string Clear(string value)
    {
        var root = JsonNode.Parse(value)!.AsObject();
        foreach (var store in root)
            if (store.Value is JsonArray rows)
                foreach (var row in rows.OfType<JsonObject>()) row.Remove("EditId");
        return root.ToJsonString();
    }
}
