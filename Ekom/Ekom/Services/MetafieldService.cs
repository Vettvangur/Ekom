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

        IEnumerable<UmbracoContent> metafieldNodes = _nodeService.NodesByTypes("ekmMetaField");

        IEnumerable<Metafield> result = metafieldNodes.Select(x => new Metafield(x));

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

        var list = new List<Metavalue>();

        var fields = GetMetafields().ToList();

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

        _cache.Set(cacheKey, list, TimeSpan.FromMinutes(360));

        return list;
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
        var valueJsonArray = string.IsNullOrWhiteSpace(json) ? new JArray() : JArray.Parse(json);
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
            // No existing metafield entry -> create new with all incoming values
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

        // Build index: id -> existing JObject
        // (Only objects with an "id" are indexed. Everything else is left untouched.)
        var existingById = existingArray
            .OfType<JObject>()
            .Where(o => !string.IsNullOrWhiteSpace(o["id"]?.ToString()))
            .ToDictionary(o => o["id"]!.ToString(), o => o);

        foreach (var incomingObj in incomingArray.OfType<JObject>())
        {
            var incomingId = incomingObj["id"]?.ToString();

            // If no id, we cannot "update same id" -> just append (no deletes).
            if (string.IsNullOrWhiteSpace(incomingId))
            {
                existingArray.Add(incomingObj);
                continue;
            }

            if (existingById.TryGetValue(incomingId, out var existingObj))
            {
                // Update-in-place: replace properties but keep the object position.
                // This avoids removing anything and keeps ordering stable.
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
        var metafields = products
         .SelectMany(x => x.Metafields)
         .Where(x => x.Field.Filterable == filterable)
         .ToList();

        var grouped = metafields.GroupBy(x => x.Field, new MetafieldComparer());

        foreach (var group in grouped)
        {
            List<Dictionary<string, string>> distinctValues = group
                .SelectMany(x => x.Values)
                .Where(x => !x.ContainsKey("undefined"))
                .DistinctBy(x => x.Values.FirstOrDefault()) // Assuming DistinctBy is efficient
                .OrderBy(x => x.Values.FirstOrDefault(), new SemiNumericComparer())
                .ToList();

            yield return new MetafieldGrouped()
            {
                Field = group.Key,
                Values = distinctValues
            };
        }
    }

    public IEnumerable<IProduct> FilterProducts(IEnumerable<IProduct> products, ProductQuery query)
    {

        if (query?.MetaFilters?.Any() == true)
        {
            var filterCriteria = query.MetaFilters;

            products = products.Where(product =>
            {
                foreach (var (key, expectedValues) in filterCriteria)
                {
                    var matchingMetafields = product.Metafields
                        .Where(metaField =>
                            metaField.Field.Id.ToString() == key ||
                            metaField.Field.Alias.Equals(key, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (!matchingMetafields.Any())
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
                                dict.Values.Any(val => expectedValues.Contains(val))));

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

        query.PropertyFilters.Remove("priceFrom");
        query.PropertyFilters.Remove("priceTo");

        return products;
    }
}
