using Ekom.Models;
using Ekom.Models.Comparers;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;

namespace EkomCore.Services
{
    class MetafieldService : IMetafieldService
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
            var cacheKey = $"GetMetafields";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<Metafield> cachedResponse))
            {
                return cachedResponse;
            }

            var metafieldNodes = _nodeService.NodesByTypes("ekmMetaField");

            var result = metafieldNodes.Select(x => new Metafield(x));

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(360));

            return result;
        }

        public List<Metavalue> SerializeMetafields(string jsonValue, int nodeId)
        {
            if (string.IsNullOrEmpty(jsonValue))
            {
                return null;
            }

            var cacheKey = $"{nodeId}_SerializeMetafields";

            if (_cache.TryGetValue(cacheKey, out List<Metavalue> cachedResponse))
            {
                return cachedResponse;
            }

            var list = new List<Metavalue>();

            var fields = GetMetafields();

            var jArray = JArray.Parse(jsonValue);

            foreach (JObject item in jArray)
            {
                if (item.ContainsKey("Key") && Guid.TryParse(item["Key"].ToString(), out Guid _metaFieldKey))
                {
                    var valuesList = new List<Dictionary<string, string>>();

                    var field = fields.FirstOrDefault(x => x.Key == _metaFieldKey);

                    if (field != null)
                    {
                        var valuesToken = item.SelectToken("Values");

                        if (valuesToken.Type == JTokenType.Array)
                        {
                            var valuesArray = valuesToken as JArray;

                            foreach (var arrayItem in valuesArray)
                            {
                                var valueObject = arrayItem as JObject;

                                if (valueObject != null && valueObject.ContainsKey("id"))
                                {
                                    var valueId = valueObject["id"].ToString();

                                    var fieldValues = field.Values.FirstOrDefault(x => x.Id == valueId);

                                    if (fieldValues != null)
                                    {
                                        valuesList.Add(fieldValues.Values.Where(x => x.Key != "undefined").ToDictionary(x => x.Key, x => x.Value));
                                    }
                                }
                            }


                        }
                        else if (valuesToken.Type == JTokenType.Object)
                        {
                            var valueObject = valuesToken as JObject;

                            if (valueObject != null && valueObject.ContainsKey("id"))
                            {
                                var valueId = valueObject["id"].ToString();

                                var fieldValues = field.Values.FirstOrDefault(x => x.Id == valueId);

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
            var metaFields = GetMetafields();

            var newArray = new JArray();

            if (!string.IsNullOrEmpty(json))
            {
                newArray = JArray.Parse(json);
            }

            foreach (var value in values)
            {
                var field = metaFields.FirstOrDefault(x => x.Alias == value.Key);

                if (field != null)
                {
                    var firstValue = value.Value.FirstOrDefault();
                    var firstSubValue = firstValue?.Values.FirstOrDefault();
                    JArray? jArrayValue = field.Values.Count > 0 ? JArray.FromObject(value) : null;

                    var newObject = new JObject
                    {
                        { "Key", new JValue(field.Key.ToString()) },
                        { "Values", jArrayValue != null ? jArrayValue : new JValue(firstSubValue?.Value) }
                    };

                    if (newArray.Count() > 0)
                    {
                        foreach (JObject item in newArray)
                        {
                            if (item.ContainsKey("Key") && Guid.TryParse(item["Key"].ToString(), out Guid _metaFieldKey))
                            {
                                // Replace
                                if (field.Key == _metaFieldKey)
                                {
                                    item["Values"] = newObject["Values"];
                                    break;
                                }

                            }
                            else
                            {
                                // Append
                                newArray.Append(newObject);
                                continue;
                            }
                        }
                    }
                    else
                    {
                        newArray.Add(newObject);
                    }

                }
            }

            return newArray;
        }

        public List<Dictionary<string, string>> GetMetaFieldValue(string json, int nodeId, string metafieldAlias)
        {
            var nodeMetaFields = SerializeMetafields(json, nodeId);

            if (nodeMetaFields == null || !nodeMetaFields.Any())
            {
                return new List<Dictionary<string, string>>();
            }

            var metaField = nodeMetaFields.FirstOrDefault(x => x.Field.Alias.Equals(metafieldAlias, StringComparison.InvariantCultureIgnoreCase));

            if (metaField == null)
            {
                return new List<Dictionary<string, string>>();
            }

            return metaField.Values;
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
            var metafields = products
             .SelectMany(x => x.Metafields)
             .Where(x => x.Field.Filterable == filterable)
             .ToList();

            var grouped = metafields.GroupBy(x => x.Field, new MetafieldComparer());

            foreach (var group in grouped)
            {
                var distinctValues = group
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

                products = products
                .Where(product => filterCriteria.All(criteria =>
                    product.Metafields.Any(metaField =>
                        metaField.Field.Id.ToString() == criteria.Key && criteria.Value.Intersect(metaField.Values.SelectMany(v => v.Values.Select(c => c).ToList())).Any()
                    )
                ));

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

                products = products.Where(product =>
                    query.PropertyFilters
                    .Where(f => !string.IsNullOrEmpty(f.Key) && f.Value != null && f.Value.Any())
                    .All(f => product.Properties.Any(p => p.Key == f.Key &&
                        f.Value.Any(d => p.Value != null && p.Value.Contains(d, StringComparison.InvariantCultureIgnoreCase)))));
            }

            return products;
        }
    }
}
