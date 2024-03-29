using Ekom.Models;
using Ekom.Services;
using Ekom.Umb.DataEditors;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Ekom.Utilities
{
    public static class ContentExtensions
    {
        /// <summary>
        /// Set Property on ekom content
        /// </summary>
        /// <param name="content">IContent</param>
        /// <param name="alias">Property alias</param>
        /// <param name="values">Values to insert</param>
        /// <param name="type">Type of property, Language or Store</param>
        public static void SetProperty(this IContent content, string alias, Dictionary<string, object> values, PropertyEditorType type = PropertyEditorType.Empty)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            if (string.IsNullOrEmpty(alias))
            {
                throw new ArgumentNullException("alias");
            }

            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            var property = content.Properties.FirstOrDefault(x => x.Alias.ToUpperInvariant() == alias.ToUpperInvariant());

            if (property != null)
            {
                var dts = Configuration.Resolver.GetService<IDataTypeService>();

                var editor = dts.GetByEditorAlias(property.PropertyType.PropertyEditorAlias);

                if (editor.Any())
                {
                    var dataType = editor.FirstOrDefault();

                    if (type == PropertyEditorType.Empty)
                    {
                        var prevalues = new EkomPropertyEditorConfiguration() { useLanguages = true };

                        try
                        {
                            prevalues = (EkomPropertyEditorConfiguration)dataType.Configuration;

                            type = prevalues.useLanguages ? PropertyEditorType.Language : PropertyEditorType.Store;
                        }
                        catch
                        {
                           
                        }
                    }

                    string value = JsonConvert.SerializeObject(new PropertyValue
                    {
                        DtdGuid = dataType.Key,
                        Values = values,
                        Type = type,
                    });
                    
                    content.SetValue(alias, value);

                    return;
                }

                throw new InvalidOperationException("Unable to get data type for property.");
            }

            throw new InvalidOperationException("Unable to find matching property on IContent.");
        }

        /// <summary>
        /// Set Slug on ekom product or category
        /// </summary>
        /// <param name="content">IContent</param>
        /// <param name="values">Values to insert</param>
        /// <param name="type">Type of property, Language or Store</param>
        public static void SetSlug(this IContent content, Dictionary<string, object> values, PropertyEditorType type = PropertyEditorType.Empty)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            if (values == null)
            {
                throw new ArgumentNullException("values");
            }

            if (content.ContentType.Alias != "ekmProduct" && content.ContentType.Alias != "ekmCategory")
            {
                throw new ArgumentNullException("Slug can only be set on ekom product or category");
            }

            var dict = new Dictionary<string, object>();


            var _umbService = Configuration.Resolver.GetService<IUmbracoService>();

            foreach (var value in values)
            {
                dict.Add(value.Key, _umbService.UrlSegment(value.Value.ToString()));
            }

            SetProperty(content, "slug", dict, type);
        }
        public static void SetMetafield(this IContent content, Dictionary<string, List<MetafieldValues>> values)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            if (content.ContentType.Alias != "ekmProduct")
            {
                throw new ArgumentNullException("Metafield can only be set on ekom product");
            }

            var valuesJson = JsonConvert.SerializeObject(values);

            var _metaService = Configuration.Resolver.GetService<IMetafieldService>();

            var metaValue = _metaService.SetMetafield(content.GetValue<string>("metafields"), values);

            var metaValueJson = JsonConvert.SerializeObject(metaValue);

            SetProperty(content, "metafields", metaValueJson);
        }

        public static List<Dictionary<string,string>> GetMetafieldValue(this IContent content, string metafieldAlias)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (string.IsNullOrEmpty(metafieldAlias))
            {
                throw new ArgumentNullException(nameof(metafieldAlias));
            }

            if (content.ContentType.Alias != "ekmProduct")
            {
                throw new ArgumentNullException("Metafield can only get value on ekom product");
            }

            var _metaService = Configuration.Resolver.GetService<IMetafieldService>();

            return _metaService.GetMetaFieldValue(content.GetValue<string>("metaFields"), content.Id, metafieldAlias);
        }

        internal static void SetProperty(this IContent content, string alias, string key, object value)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            if (string.IsNullOrEmpty(alias))
            {
                throw new ArgumentNullException("alias");
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }

            var property = content.Properties.FirstOrDefault(x => x.Alias.ToUpperInvariant() == alias.ToUpperInvariant());

            if (property != null)
            {
                var ekomProperty = content.GetEkomProperty(alias);

                var dictionary = new Dictionary<string, object>();
                if (ekomProperty != null && ekomProperty.Values != null && ekomProperty.Values.Any())
                {
                    foreach (KeyValuePair<string, object> value3 in ekomProperty.Values)
                    {
                        object value2 = ((value3.Key.ToUpperInvariant() == key.ToUpperInvariant()) ? value : value3.Value);
                        dictionary.Add(value3.Key, value2);
                    }
                }
                else
                {
                    dictionary.Add(key, value);
                }

                content.SetProperty(alias, dictionary);

                return;
            }

            throw new InvalidOperationException("Unable to find matching property on IContent.");
        }

        /// <summary>
        /// Set Property on content with default SetValue for ekom nodes
        /// </summary>
        /// <param name="content">IContent</param>
        /// <param name="alias">Property alias</param>
        /// <param name="value">Value to insert</param>
        public static void SetProperty(this IContent content, string alias, object value)
        {
            content.SetValue(alias, value, null);
        }

        internal static PropertyValue GetEkomProperty(this IContent content, string alias)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            var property = content.Properties.FirstOrDefault(x => x.Alias.ToUpperInvariant() == alias.ToUpperInvariant());

            if (property == null)
            {
                throw new InvalidOperationException("Unable to find matching property on IContent.");
            }

            if (property.GetValue() != null)
            {
                try
                {
                    var propertyValue = property.GetValue().ToString();

                    // Price fix. Values not added on publish. Needs a permament fix
                    if (!string.IsNullOrEmpty(propertyValue) && !propertyValue.InvariantContains("values")) {
                        propertyValue = "{\"values\":" + propertyValue + "}";
                    }

                    return JsonConvert.DeserializeObject<PropertyValue>(propertyValue);
                } catch(JsonException ex)
                {
                    return null;
                   
                }
            }

            return null;
        }

        /// <summary>
        /// Get Ekom Property
        /// </summary>
        /// <param name="content">IContent</param>
        /// <param name="alias">Property alias</param>
        /// <param name="key">Language or store key</param>
        /// <returns>Ekom property string value</returns>
        public static string GetProperty(this IContent content, string alias, string key)
        {
            if (content == null)
            {
                throw new ArgumentNullException("content");
            }

            var property = GetEkomProperty(content, alias);

            if (property != null && property.Values != null && property.Values.ContainsKey(key))
            {
                return property.Values.FirstOrDefault(x => x.Key == key).Value.ToString();
            }

            return "";
            
        }

        /// <summary>
        /// Set price on ekom product or variant
        /// </summary>
        /// <param name="content">IContent</param>
        /// <param name="storeAlias">Store alias</param>
        /// <param name="currency">Currency (is-IS, en-US)</param>
        /// <param name="price">Price as decimal</param>
        public static void SetPrice(this IContent content, string storeAlias, string currency, decimal price)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (content.HasProperty("price"))
            {

                var fieldValue = content.GetValue<string>("price");

                CurrencyPrice priceObject = null;
                CurrencyPriceRoot currencyPriceRoot = new CurrencyPriceRoot();

                try
                {
                    currencyPriceRoot = string.IsNullOrEmpty(fieldValue) ? currencyPriceRoot : JsonConvert.DeserializeObject<CurrencyPriceRoot>(fieldValue);
                    if (currencyPriceRoot.ContainsKey(storeAlias))
                    {
                        var storeItems = currencyPriceRoot[storeAlias];

                        // Ensure the storeItems list is not null and has elements
                        if (storeItems.Any())
                        {
                            // Find the first item with the specified currency
                            priceObject = storeItems.FirstOrDefault(z => z.Currency == currency);

                            if (priceObject != null)
                            {
                                priceObject.Price = price;
                            }
                            else
                            {
                                storeItems.Add(new CurrencyPrice(price, currency));
                            }
                        }
                        else
                        {
                            storeItems.Add(new CurrencyPrice(price, currency));
                        }
                    }
                    else
                    {
                        currencyPriceRoot.Add(storeAlias,
                            new List<CurrencyPrice>()
                            {
                                new(price, currency)
                            });
                    }

                    content.SetValue("price", JsonConvert.SerializeObject(currencyPriceRoot));

                    return;

                }
                catch
                {

                }

                // Fallback
                var stores = API.Store.Instance.GetAllStores();

                foreach (var store in stores)
                {
                    var currencyPrices = new List<CurrencyPrice>();

                    if (!string.IsNullOrEmpty(fieldValue))
                    {
                        try
                        {
                            var jsonCurrencyValue = fieldValue.GetEkomPropertyEditorValue(storeAlias);

                            currencyPrices = jsonCurrencyValue.GetCurrencyPrices();

                        }
                        catch
                        {

                        }
                    }

                    if (storeAlias == store.Alias)
                    {
                        if (currencyPrices.Any(x => x.Currency == currency))
                        {
                            currencyPrices.FirstOrDefault(x => x.Currency == currency).Price = price;

                        }
                        else
                        {
                            currencyPrices.Add(new CurrencyPrice(price, currency));
                        }
                    }


                    currencyPriceRoot.Add(store.Alias, currencyPrices);
                }

                content.SetValue("price", JsonConvert.SerializeObject(currencyPriceRoot));
            }

        }

        /// <summary>
        /// Get Price from product or variant
        /// </summary>
        /// <param name="content">IContent</param>
        /// <param name="storeAlias">Store alias</param>
        /// <param name="currency">Currency (is-IS, en-US)</param>
        /// <returns>Ekom Price as decimal</returns>
        public static decimal GetPrice(this IContent content, string storeAlias, string currency)
        {
            var fieldValue = content.GetProperty("price", storeAlias);

            if (!string.IsNullOrEmpty(fieldValue))
            {
                var currencyValues = fieldValue.GetCurrencyValues();

                var value = string.IsNullOrEmpty(currency) ? currencyValues.FirstOrDefault() : currencyValues.FirstOrDefault(x => x.Currency == currency);

                return value != null ? value.Value : 0;
            }

            return 0;
        }
    }
}
