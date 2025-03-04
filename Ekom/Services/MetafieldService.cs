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

    public List<Metavalue> SerializeMetafields(string jsonValue, int nodeId)
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

        List<Metavalue> list = new List<Metavalue>();

        IEnumerable<Metafield> fields = GetMetafields();

        JArray jArray = JArray.Parse(jsonValue);

        foreach (JObject item in jArray)
        {
            if (item.ContainsKey("Key") && Guid.TryParse(item["Key"].ToString(), out Guid _metaFieldKey))
            {
                List<Dictionary<string, string>> valuesList = new List<Dictionary<string, string>>();

                Metafield? field = fields.FirstOrDefault(x => x.Key == _metaFieldKey);

                if (field != null)
                {
                    JToken? valuesToken = item.SelectToken("Values");

                    if (valuesToken.Type == JTokenType.Array)
                    {
                        JArray? valuesArray = valuesToken as JArray;

                        foreach (JToken arrayItem in valuesArray)
                        {
                            JObject? valueObject = arrayItem as JObject;

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
                .GroupBy(item => item["Key"]?.ToString())
                .Select(group => group.First())
                .ToList();

            valueJsonArray = new JArray(distinctItems);
        }

        foreach (KeyValuePair<string, List<MetafieldValues>> value in values)
        {
            Metafield? field = metaFields.FirstOrDefault(x => x.Alias == value.Key);

            if (field != null)
            {
                MetafieldValues? firstValue = value.Value.FirstOrDefault();
                KeyValuePair<string, string>? firstSubValue = firstValue?.Values.FirstOrDefault();
                JArray? jArrayValue = field.Values.Count > 0 ? JArray.FromObject(value) : null;

                JObject newObject = new JObject
                {
                    { "Key", new JValue(field.Key.ToString()) },
                    { "Values", jArrayValue != null ? jArrayValue : new JValue(firstSubValue?.Value) }
                };

                // If any value exist in the array
                if (valueJsonArray.Count() > 0)
                {
                    bool containsKey = valueJsonArray.Any(item => item["Key"]?.ToString() == field.Key.ToString());

                    if (!containsKey)
                    {
                        // Append Object if the key is not in the existing list
                        valueJsonArray.Add(newObject);
                    }
                    else
                    {
                        JObject? targetObject = valueJsonArray.FirstOrDefault(item => item["Key"]?.ToString() == field.Key.ToString()) as JObject;

                        // If found, update its value
                        if (targetObject != null)
                        {
                            targetObject["Values"] = newObject["Values"];
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
        List<Metavalue> nodeMetaFields = SerializeMetafields(json, nodeId);

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

    public string GetMetaFieldValue(IProduct product, string metafieldAlias, string culture = "")
    {
        List<Metavalue> nodeMetaFields = product.Metafields;

        if (nodeMetaFields == null || !nodeMetaFields.Any())
        {
            return string.Empty;
        }

        Metavalue? metaField = nodeMetaFields.FirstOrDefault(x => x.Field.Alias.Equals(metafieldAlias, StringComparison.InvariantCultureIgnoreCase));

        if (metaField == null)
        {
            return string.Empty;
        }

        if (metaField.Values.Any(x => x.ContainsKey("")))
        {
            return metaField.Values.FirstOrDefault()?.Values.FirstOrDefault();
        }

        if (metaField.Values.Any(x => x.ContainsKey(culture)))
        {
            return string.Join(",", metaField.Values.Where(x => x.ContainsKey(culture)).Select(d => d.GetValue(culture)));
        }

        return metaField.Values.FirstOrDefault()?.Values.FirstOrDefault();
    }

    public IEnumerable<MetafieldGrouped> Filters(IEnumerable<IProduct> products, bool filterable = true)
    {
        List<Metavalue> metafields = products
         .SelectMany(x => x.Metafields)
         .Where(x => x.Field.Filterable == filterable)
         .ToList();

        IEnumerable<IGrouping<Metafield, Metavalue>> grouped = metafields.GroupBy(x => x.Field, new MetafieldComparer());

        foreach (IGrouping<Metafield, Metavalue> group in grouped)
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
            Dictionary<string, List<string>> filterCriteria = query.MetaFilters;

            products = products.Where(product =>
            {
                // Check if all filter criteria are met for this product
                return filterCriteria.All(criteria =>
                {
                    // Find the matching metafields for the current criteria
                    IEnumerable<Metavalue> matchingMetafields = product.Metafields.Where(metaField =>
                        metaField.Field.Id.ToString() == criteria.Key
                    );

                    // Get the AllConditionsMustMatch flag from the metafield
                    bool allConditionsMustMatch = matchingMetafields.Any(metaField => metaField.Field.AllConditionsMustMatch);

                    if (allConditionsMustMatch)
                    {
                        // Use AND logic: all values must match
                        return matchingMetafields.All(metaField =>
                            criteria.Value.All(value =>
                                metaField.Values.SelectMany(v => v.Values).Contains(value)
                            )
                        );
                    }
                    else
                    {
                        // Use OR logic: any value can match
                        return matchingMetafields.Any(metaField =>
                            criteria.Value.Intersect(
                                metaField.Values.SelectMany(v => v.Values)
                            ).Any()
                        );
                    }
                });
            });

            //products = products
            //    .Where(x =>
            //        x.Metafields.Any(metaField =>
            //            query.MetaFilters.Where(filter => filter.Value != null && filter.Value.Any())
            //            .All(filter =>
            //                filter.Key == metaField.Field.Id.ToString() &&
            //                filter.Value.Intersect(metaField.Values.SelectMany(v => v.Values.Select(c => c).ToList())).Any()
            //            )
            //        )
            //);
        }

        if (query?.PropertyFilters?.Any() == true)
        {

            products = FilterByPrice(products, query);

            products = products.Where(product =>
                query.PropertyFilters
                    .Where(f => !string.IsNullOrEmpty(f.Key) && f.Value != null && f.Value.Any())
                    .All(f => product.Properties.Any(p => p.Key == f.Key &&
                        p.Value != null &&
                        p.Value.Split(new[] { query.PropertySelectorsSeparator }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(value => value.Trim()) // Trim each split value
                               .Any(splitValue => f.Value.Any(d => splitValue.Equals(d, StringComparison.InvariantCultureIgnoreCase))))));
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
