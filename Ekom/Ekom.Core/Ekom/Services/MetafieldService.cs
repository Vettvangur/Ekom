using Ekom.Models;
using Ekom.Models.Comparers;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;

namespace EkomCore.Services;

internal class MetafieldService : IMetafieldService
{
    private readonly INodeService _nodeService;
    private IMemoryCache _cache;
    public MetafieldService(INodeService nodeService, IMemoryCache cache)
    {
        _nodeService = nodeService;
        _cache = cache;
    }

    public IEnumerable<Metafield> GetMetafields()
    {
        string cacheKey = $"GetMetafields";

        if (_cache.TryGetValue(cacheKey, out IEnumerable<Metafield> cachedResponse))
        {
            return cachedResponse;
        }

        IEnumerable<UmbracoContent> metafieldNodes = _nodeService.NodesByTypes("ekmMetafield");

        // Materialize before caching. A deferred query would map every definition again on each
        // enumeration (once per product) and keep this request's node service alive in the cache.
        IEnumerable<Metafield> result = metafieldNodes.Select(x => new Metafield(x)).ToList();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(360));

        return result;
    }

    public List<Metavalue>? SerializeMetafields(string jsonValue, int nodeId)
    {
        if (string.IsNullOrEmpty(jsonValue))
        {
            return null;
        }

        string cacheKey = $"{nodeId}_SerializeMetafields";

        if (_cache.TryGetValue(cacheKey, out List<Metavalue> cachedResponse))
        {
            return cachedResponse;
        }

        var fields = GetMetafields() as List<Metafield> ?? GetMetafields().ToList();

        // Plain picker output is read without building a JToken tree; anything unusual
        // (numbers, dates, malformed or lenient JSON) is left to the JToken parser.
        if (!TryParseMetafields(jsonValue, fields, out var list))
            list = ParseMetafieldsWithJToken(jsonValue, fields);

        _cache.Set(cacheKey, list, TimeSpan.FromMinutes(360));

        return list;
    }

    internal static List<Metavalue> ParseMetafieldsWithJToken(string jsonValue, List<Metafield> fields)
    {
        var list = new List<Metavalue>();

        var jArray = JArray.Parse(jsonValue);
        jArray = (JArray)JsonHelper.ToCamelCaseKeys(jArray);

        foreach (JObject item in jArray)
        {
            if (item.ContainsKey("key") && Guid.TryParse(item["key"].ToString(), out Guid _metaFieldKey))
            {
                List<Dictionary<string, string>> valuesList = new List<Dictionary<string, string>>();

                Metafield? field = fields.FirstOrDefault(x => x.Key == _metaFieldKey);

                if (field != null)
                {
                    JToken? valuesToken = item.SelectToken("values");

                    if (valuesToken.Type == JTokenType.Array)
                    {
                        JArray? valuesArray = valuesToken as JArray;

                        foreach (JToken arrayItem in valuesArray)
                        {
                            JObject? valueObject = arrayItem as JObject;

                            if (valueObject != null && valueObject.ContainsKey("id"))
                            {
                                var valueId = valueObject["id"]?.ToString();

                                MetafieldValues? fieldValues = field.Values.FirstOrDefault(x => x.Id == valueId);

                                if (fieldValues != null)
                                {
                                    valuesList.Add(fieldValues.Values.Where(x => x.Key != "undefined").ToDictionary(x => x.Key, x => x.Value));
                                }
                            }
                        }


                    }
                    else if (valuesToken.Type == JTokenType.Object)
                    {
                        JObject? valueObject = valuesToken as JObject;

                        if (valueObject != null && valueObject.ContainsKey("id"))
                        {
                            string valueId = valueObject["id"].ToString();

                            MetafieldValues? fieldValues = field.Values.FirstOrDefault(x => x.Id == valueId);

                            if (fieldValues != null)
                            {
                                valuesList.Add(fieldValues.Values.Where(x => x.Key != "undefined").ToDictionary(x => x.Key, x => x.Value));
                            }
                        }

                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(valuesToken.ToString()))
                        {
                            valuesList.Add(new Dictionary<string, string>() {
                                { "", valuesToken.ToString() } });
                        }
                    }

                    if (valuesList.Any())
                    {
                        list.Add(new Metavalue()
                        {
                            Field = field,
                            Values = valuesList
                        });
                    }

                }

            }
        }

        return list;
    }

    internal static bool TryParseMetafields(string jsonValue, List<Metafield> fields, out List<Metavalue> list)
    {
        list = new List<Metavalue>();

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(jsonValue);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                return false;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return false;

                if (!TryGetProperty(item, "key", out var keyElement))
                    continue;

                if (!TryReadText(keyElement, out var keyText))
                    return false;

                if (!Guid.TryParse(keyText, out var fieldKey))
                    continue;

                Metafield? field = null;
                foreach (var candidate in fields)
                {
                    if (candidate.Key == fieldKey)
                    {
                        field = candidate;
                        break;
                    }
                }

                if (field == null)
                    continue;

                if (!TryGetProperty(item, "values", out var values))
                    return false;

                var valuesList = new List<Dictionary<string, string>>();
                if (values.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var arrayItem in values.EnumerateArray())
                    {
                        if (arrayItem.ValueKind == System.Text.Json.JsonValueKind.Object && !TryAddPickedValue(arrayItem, field, valuesList))
                            return false;
                    }
                }
                else if (values.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (!TryAddPickedValue(values, field, valuesList))
                        return false;
                }
                else
                {
                    if (!TryReadText(values, out var text))
                        return false;

                    if (!string.IsNullOrEmpty(text))
                        valuesList.Add(new Dictionary<string, string>() { { "", text } });
                }

                if (valuesList.Count > 0)
                {
                    list.Add(new Metavalue()
                    {
                        Field = field,
                        Values = valuesList
                    });
                }
            }

            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static bool TryAddPickedValue(System.Text.Json.JsonElement value, Metafield field, List<Dictionary<string, string>> valuesList)
    {
        if (!TryGetProperty(value, "id", out var idElement))
            return true;

        if (!TryReadText(idElement, out var valueId))
            return false;

        foreach (var fieldValues in field.Values)
        {
            if (fieldValues.Id != valueId)
                continue;

            var copy = new Dictionary<string, string>(fieldValues.Values);
            copy.Remove("undefined");
            valuesList.Add(copy);
            break;
        }

        return true;
    }

    /// <summary>
    /// Finds a property the way the camel-cased JObject does: "Key" and "key" are the same
    /// name and the last one wins.
    /// </summary>
    private static bool TryGetProperty(System.Text.Json.JsonElement element, string camelName, out System.Text.Json.JsonElement value)
    {
        value = default;
        var found = false;
        foreach (var property in element.EnumerateObject())
        {
            var name = property.Name;
            if (name.Length != camelName.Length
                || char.ToLowerInvariant(name[0]) != camelName[0]
                || !name.AsSpan(1).SequenceEqual(camelName.AsSpan(1))
                || (name[0] != camelName[0] && !char.IsUpper(name[0])))
            {
                continue;
            }

            value = property.Value;
            found = true;
        }

        return found;
    }

    /// <summary>The text JToken.ToString() gives for a string or null. Other kinds are not handled here.</summary>
    private static bool TryReadText(System.Text.Json.JsonElement element, out string text)
    {
        text = string.Empty;
        if (element.ValueKind == System.Text.Json.JsonValueKind.Null)
            return true;

        if (element.ValueKind != System.Text.Json.JsonValueKind.String)
            return false;

        text = element.GetString() ?? string.Empty;

        // JArray.Parse turns date-like strings into dates and formats them differently.
        return !(text.Length > 0
            && (text[0] == '/' || (text.Length >= 19 && text.Length <= 40 && char.IsDigit(text[0]) && text[10] == 'T')));
    }

    public JArray SetMetafield(string json, Dictionary<string, List<MetafieldValues>> values)
    {
        IEnumerable<Metafield> metaFields = GetMetafields();

        JArray valueJsonArray = new JArray();

        if (!string.IsNullOrEmpty(json))
        {
            valueJsonArray = JArray.Parse(json);

            // Remove duplicates
            List<JToken> distinctItems = valueJsonArray
                .GroupBy(item => item["key"]?.ToString())
                .Select(group => group.First())
                .ToList();

            valueJsonArray = new JArray(distinctItems);
            valueJsonArray = (JArray)JsonHelper.ToCamelCaseKeys(valueJsonArray);
        }

        foreach (KeyValuePair<string, List<MetafieldValues>> value in values)
        {
            Metafield? field = metaFields.FirstOrDefault(x => x.Alias == value.Key);

            if (field != null)
            {
                MetafieldValues? firstValue = value.Value.FirstOrDefault();
                KeyValuePair<string, string>? firstSubValue = firstValue?.Values.FirstOrDefault();
                var jArrayValue = field.Values.Count > 0 ? JArray.FromObject(value.Value) : null;
                jArrayValue = (JArray)JsonHelper.ToCamelCaseKeys(jArrayValue);

                var newObject = new JObject
                {
                    { "key", new JValue(field.Key.ToString()) },
                    { "values", jArrayValue != null ? jArrayValue : new JValue(firstSubValue?.Value) }
                };

                // If any value exist in the array
                if (valueJsonArray.Count() > 0)
                {
                    bool containsKey = valueJsonArray.Any(item => item["key"]?.ToString() == field.Key.ToString());

                    if (!containsKey)
                    {
                        // Append Object if the key is not in the existing list
                        valueJsonArray.Add(newObject);
                    }
                    else
                    {
                        JObject? targetObject = valueJsonArray.FirstOrDefault(item => item["key"]?.ToString() == field.Key.ToString()) as JObject;

                        // If found, update its value
                        if (targetObject != null)
                        {
                            targetObject["values"] = newObject["values"];
                        }
                    }

                }
                else
                {
                    valueJsonArray.Add(newObject);
                }

            }
        }

        return valueJsonArray;
    }

    public List<Dictionary<string, string>> GetMetaFieldValue(string json, int nodeId, string metafieldAlias)
    {
        var nodeMetaFields = SerializeMetafields(json, nodeId);

        if (nodeMetaFields == null || !nodeMetaFields.Any())
        {
            return new List<Dictionary<string, string>>();
        }

        Metavalue? metaField = nodeMetaFields.FirstOrDefault(x => x.Field.Alias.Equals(metafieldAlias, StringComparison.InvariantCultureIgnoreCase));

        if (metaField == null)
        {
            return new List<Dictionary<string, string>>();
        }

        return metaField.Values;
    }

    public JArray AppendMetafield(
     string? json,
     string metafieldAlias,
     IEnumerable<MetafieldValues> incomingValues)
    {
        var metaFields = GetMetafields().ToList();

        var field = metaFields.FirstOrDefault(x =>
            x.Alias.Equals(metafieldAlias, StringComparison.InvariantCultureIgnoreCase));

        // If metafield doesn't exist in Umbraco definition, just return parsed json unchanged
        if (field == null)
            return string.IsNullOrWhiteSpace(json) ? new JArray() : JArray.Parse(json);

        // Current JSON array for node
        var valueJsonArray = string.IsNullOrWhiteSpace(json)
            ? new JArray()
            : JArray.Parse(json);

        valueJsonArray = (JArray)JsonHelper.ToCamelCaseKeys(valueJsonArray);

        // Find existing metafield object by key (guid string)
        var targetObject = valueJsonArray
            .OfType<JObject>()
            .FirstOrDefault(o => o["key"]?.ToString() == field.Key.ToString());

        // Convert incoming values to JArray (camelCase)
        var incomingArray = JArray.FromObject(incomingValues?.ToList() ?? new List<MetafieldValues>());
        incomingArray = (JArray)JsonHelper.ToCamelCaseKeys(incomingArray);

        if (targetObject == null)
        {
            valueJsonArray.Add(new JObject
            {
                ["key"] = field.Key.ToString(),
                ["values"] = incomingArray
            });

            return valueJsonArray;
        }

        // Ensure existing "values" is an array
        var existingToken = targetObject["values"];
        var existingArray = existingToken as JArray ?? new JArray();
        existingArray = (JArray)JsonHelper.ToCamelCaseKeys(existingArray);

        var existingById = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var o in existingArray.OfType<JObject>())
        {
            var id = o["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id))
                continue;

            // Keep FIRST occurrence indexed
            if (!existingById.ContainsKey(id))
                existingById[id] = o;
        }

        var incomingById = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var incomingObj in incomingArray.OfType<JObject>())
        {
            var id = incomingObj["id"]?.ToString();

            if (string.IsNullOrWhiteSpace(id))
            {
                // No id → always append
                existingArray.Add(incomingObj);
                continue;
            }

            incomingById[id] = incomingObj; // last wins
        }

        // -----------------------------
        // MERGE
        // -----------------------------
        foreach (var kv in incomingById)
        {
            var incomingId = kv.Key;
            var incomingObj = kv.Value;

            if (existingById.TryGetValue(incomingId, out var existingObj))
            {
                // Update in place (preserve position)
                existingObj.RemoveAll();
                foreach (var prop in incomingObj.Properties())
                    existingObj.Add(prop.Name, prop.Value);
            }
            else
            {
                // Append new
                existingArray.Add(incomingObj);
                existingById[incomingId] = incomingObj;
            }
        }

        targetObject["values"] = existingArray;
        return valueJsonArray;
    }
    public string GetMetaFieldValue(IProduct product, string metafieldAlias, string culture = "")
    {
        var nodeMetaFields = product.Metafields;

        if (nodeMetaFields == null || !nodeMetaFields.Any())
        {
            return string.Empty;
        }

        var metaField = nodeMetaFields.FirstOrDefault(x => x.Field.Alias.Equals(metafieldAlias, StringComparison.InvariantCultureIgnoreCase));

        if (metaField == null)
        {
            return string.Empty;
        }

        if (metaField.Values.Any(x => x.ContainsKey("")))
        {
            return metaField.Values.FirstOrDefault()?.Values.FirstOrDefault() ?? "";
        }

        if (metaField.Values.Any(x => x.ContainsKey(culture)))
        {
            return string.Join(",", metaField.Values.Where(x => x.ContainsKey(culture)).Select(d => d.GetValue(culture)));
        }

        return metaField.Values.FirstOrDefault()?.Values.FirstOrDefault() ?? "";
    }

    public IEnumerable<MetafieldGrouped> Filters(IEnumerable<IProduct> products, bool filterable = true)
    {
        // One pass over the products: fields in first-seen order, each with its distinct values
        // (by the first language value, first occurrence wins).
        var groups = new Dictionary<Guid, FacetGroup>();
        var ordered = new List<FacetGroup>();

        foreach (var product in products)
        {
            foreach (var metavalue in product.Metafields)
            {
                var field = metavalue.Field;
                if (field.Filterable != filterable)
                    continue;

                if (!groups.TryGetValue(field.Key, out var group))
                {
                    group = new FacetGroup(field);
                    groups.Add(field.Key, group);
                    ordered.Add(group);
                }

                foreach (var value in metavalue.Values)
                {
                    if (!value.ContainsKey("undefined") && group.Seen.Add(FirstValue(value)))
                        group.Values.Add(value);
                }
            }
        }

        var comparer = new SemiNumericComparer();
        foreach (var group in ordered)
        {
            yield return new MetafieldGrouped()
            {
                Field = group.Field,
                Values = group.Values.OrderBy(FirstValue, comparer).ToList()
            };
        }
    }

    private static string? FirstValue(Dictionary<string, string> value)
    {
        foreach (var item in value.Values)
            return item;

        return null;
    }

    private sealed class FacetGroup(MetafieldSlim field)
    {
        public MetafieldSlim Field { get; } = field;
        public HashSet<string?> Seen { get; } = new();
        public List<Dictionary<string, string>> Values { get; } = new();
    }

    public IEnumerable<IProduct> FilterProducts(IEnumerable<IProduct> products, ProductQuery query)
    {

        if (query?.MetaFilters?.Any() == true)
        {
            // Resolve each filter once, not once per product: the key is a metafield id or alias.
            var filterCriteria = query.MetaFilters
                .Select(filter => (
                    Key: filter.Key,
                    Id: int.TryParse(filter.Key, out var id) && id.ToString() == filter.Key ? id : (int?)null,
                    Values: filter.Value,
                    ValueSet: new HashSet<string>(filter.Value ?? [])))
                .ToList();

            products = products.Where(product =>
            {
                foreach (var (key, fieldId, expectedValues, expectedSet) in filterCriteria)
                {
                    var matchingMetafields = new List<MetavalueSlim>();
                    foreach (var metaField in product.Metafields)
                    {
                        if ((fieldId.HasValue && metaField.Field.Id == fieldId.Value) ||
                            metaField.Field.Alias.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            matchingMetafields.Add(metaField);
                        }
                    }

                    if (matchingMetafields.Count == 0)
                        return false;

                    bool allConditionsMustMatch = matchingMetafields.Any(mf => mf.Field.AllConditionsMustMatch);

                    if (allConditionsMustMatch)
                    {
                        // AND logic: all values must match across all matching metafields
                        foreach (var value in expectedValues)
                        {
                            bool valueMatched = matchingMetafields.Any(metaField =>
                                metaField.Values.Any(dict =>
                                    dict.Values.Contains(value)));

                            if (!valueMatched)
                                return false;
                        }
                    }
                    else
                    {
                        // OR logic: any value must match
                        bool matched = matchingMetafields.Any(metaField =>
                            metaField.Values.Any(dict =>
                                dict.Values.Any(val => expectedSet.Contains(val))));

                        if (!matched)
                            return false;
                    }
                }

                return true;
            });
        }

        if (query?.PropertyFilters?.Any() == true)
        {

            products = FilterByPrice(products, query);
            products = FilterByCategory(products, query);

            var filters = query.PropertyFilters
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is { } vs && vs.Any())
                .ToDictionary(
                    kv => kv.Key,
                    kv => new HashSet<string>(kv.Value.Select(v => v?.Trim() ?? ""), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase
                );

            static IEnumerable<string> Tokenize(string? raw, string sep)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    return Enumerable.Empty<string>();

                var s = raw.Trim();

                // 1) JSON array like ["Ristretto","Espresso"]
                if (s.StartsWith("[") && s.EndsWith("]"))
                {
                    try
                    {
                        return JArray.Parse(s)
                                    .Values<string>()
                                    .Where(v => !string.IsNullOrWhiteSpace(v))!;
                    }
                    catch
                    {
                        // fall through
                    }
                }

                // 2) Quoted CSV like "Ristretto","Espresso"
                if (s.StartsWith("\"") && s.EndsWith("\"") && s.Contains("\",\""))
                {
                    s = s.Trim('"'); // remove outer quotes
                    return s.Split(new[] { "\",\"" }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => x.Replace("\\\"", "\"").Trim())
                            .Where(x => x.Length > 0);
                }

                // 3) Plain separator
                return s.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim().Trim('"').Replace("\\\"", "\""))
                        .Where(x => x.Length > 0);
            }

            products = products.Where(product =>
            {
                foreach (var (key, wantedSet) in filters.Where(x => !string.IsNullOrEmpty(x.Key)))
                {
                    var propValue = product.Properties.GetValue(key);


                    if (string.IsNullOrEmpty(propValue))
                        return false;

                    var tokens = Tokenize(propValue, query.PropertySelectorsSeparator);

                    // must match ANY of the desired values for this key
                    if (!tokens.Any(t => wantedSet.Contains(t)))
                        return false;
                }
                return true;
            });
        }

        return products;
    }

    private IEnumerable<IProduct> FilterByCategory(IEnumerable<IProduct> products, ProductQuery query)
    {
        var categoryFilter = query.PropertyFilters.FirstOrDefault(x => x.Key.Equals("category", StringComparison.OrdinalIgnoreCase));
        if (categoryFilter.Value == null || !categoryFilter.Value.Any())
            return products;

        var categoryIds = categoryFilter.Value
            .SelectMany(x => x.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(x => int.TryParse(x, out _))
            .Select(int.Parse)
            .ToHashSet();

        RemovePropertyFilter(query, "category");

        if (categoryIds.Count == 0)
            return products;

        return products.Where(product => product.Categories
            .Concat(product.CategoryAncestors)
            .Any(category => categoryIds.Contains(category.Id)));
    }

    private IEnumerable<IProduct> FilterByPrice(IEnumerable<IProduct> products, ProductQuery query)
    {

        // Retrieve the priceFrom and priceTo filters
        KeyValuePair<string, List<string>> priceFromFilter = query.PropertyFilters.FirstOrDefault(x => x.Key == "priceFrom");
        KeyValuePair<string, List<string>> priceToFilter = query.PropertyFilters.FirstOrDefault(x => x.Key == "priceTo");

        decimal? priceFrom = null;
        decimal? priceTo = null;

        // Parse the first value of priceFrom if it exists
        if (priceFromFilter.Value != null && priceFromFilter.Value.Any() &&
            decimal.TryParse(priceFromFilter.Value.First(), out decimal parsedPriceFrom))
        {
            priceFrom = parsedPriceFrom;
        }

        // Parse the first value of priceTo if it exists
        if (priceToFilter.Value != null && priceToFilter.Value.Any() &&
            decimal.TryParse(priceToFilter.Value.First(), out decimal parsedPriceTo))
        {
            priceTo = parsedPriceTo;
        }

        // Apply filtering on products based on Price.OriginalValue only if priceFrom or priceTo has a value
        if (priceFrom.HasValue || priceTo.HasValue)
        {
            products = products.Where(product =>
            {
                decimal productPrice = product.OriginalPrice.Value;

                // Filter based on priceFrom and priceTo values
                bool isWithinRange = true;

                if (priceFrom.HasValue)
                {
                    isWithinRange &= productPrice >= priceFrom.Value;
                }

                if (priceTo.HasValue)
                {
                    isWithinRange &= productPrice <= priceTo.Value;
                }

                return isWithinRange;
            });
        }

        RemovePropertyFilter(query, "priceFrom");
        RemovePropertyFilter(query, "priceTo");

        return products;
    }

    private static void RemovePropertyFilter(ProductQuery query, string key)
    {
        var matchingKey = query.PropertyFilters.Keys.FirstOrDefault(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (matchingKey != null)
            query.PropertyFilters.Remove(matchingKey);
    }
}
