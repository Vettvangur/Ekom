using Ekom.Models;
using Ekom.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EkomCore.Services
{
    class MetafieldService : IMetafieldService
    {
        private readonly INodeService _nodeService;
        public MetafieldService(INodeService nodeService)
        {
            _nodeService = nodeService;
        }

        public IEnumerable<Metafield> GetMetafields()
        {
            var metafieldNodes = _nodeService.NodesByTypes("ekmMetaField");

            return metafieldNodes.Select(x => new Metafield(x));
        }

        public List<Metavalue> SerializeMetafields(string jsonValue)
        {
            if (string.IsNullOrEmpty(jsonValue))
            {
                return null;
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

                                if (valueObject != null && valueObject.ContainsKey("Id"))
                                {
                                    var valueId = valueObject["Id"].ToString();

                                    var fieldValues = field.Values.FirstOrDefault(x => x.Id == valueId);

                                    if (fieldValues != null)
                                    {
                                        valuesList.Add(fieldValues.Values);
                                    }
                                }
                            }


                        }
                        else if (valuesToken.Type == JTokenType.Object)
                        {
                            var valueObject = valuesToken as JObject;

                            if (valueObject != null && valueObject.ContainsKey("Id"))
                            {
                                var valueId = valueObject["Id"].ToString();

                                var fieldValues = field.Values.FirstOrDefault(x => x.Id == valueId);

                                if (fieldValues != null)
                                {
                                    valuesList.Add(fieldValues.Values);
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

        public IEnumerable<MetafieldGrouped> Filters(IEnumerable<IProduct> products, bool filterable = true) {

            var list = new List<MetafieldGrouped>();

            var grouped = products
                .SelectMany(x => x.Metafields)
                .Where(x => x.Field.Filterable == filterable)
                .GroupBy(x => x.Field, new MetafieldComparer());

            foreach (var group in grouped)
            {

                list.Add(new MetafieldGrouped()
                {
                    Field = group.Key,
                    Values = group
                    .SelectMany(x => x.Values)
                    .GroupBy(x => x.Values.FirstOrDefault())
                    .Select(x => x.FirstOrDefault())
                    .ToList()
                });
            }

            return list;
        }

        public IEnumerable<IProduct> FilterProducts(IEnumerable<IProduct> products, ProductQuery query) 
        {

            if (query?.MetaFilters?.Any() == true)
            {
                products = products
                    .Where(x =>
                        x.Metafields.Any(metaField =>
                            query.MetaFilters.Where(filter => filter.Value != null && filter.Value.Any()).Any(filter =>
                                filter.Key == metaField.Field.Id.ToString() &&
                                filter.Value.Intersect(metaField.Values.SelectMany(v => v.Values.Select(c => c).ToList())).Any()
                                )
                        )
                );
            }

            if (query?.PropertyFilters?.Any() == true)
            {
                products = products.Where(x => 
                x.Properties.Any(p => 
                    query.PropertyFilters.Where(f => 
                        !string.IsNullOrEmpty(f.Key) && f.Value != null && f.Value.Any() && !string.IsNullOrEmpty(p.Value)).Any(filter =>
                        filter.Key == p.Key &&
                        filter.Value.Any(d => p.Value.Contains(d))))
                );
            }

            return products;
        } 
    }
}
