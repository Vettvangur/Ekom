using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ekom.API;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Services;
using Ekom.Utilities;
using Umbraco.Web;

namespace Ekom.Models
{
    public class OrderedProduct
    {
        private string productJson;
        private StoreInfo storeInfo;


        public delegate void CustomOrderProductPriceEventHandler(CustomOrderProductPriceEventArgs e);

        /// <summary>
        /// Occurs before product is binded to OrderedProduct
        /// </summary>
        public static event CustomOrderProductPriceEventHandler CustomOrderProductPriceEvent;


        public delegate void CustomOrderVariantPriceEventHandler(CustomOrderVariantPriceEventArgs e);

        /// <summary>
        /// Occurs before product is binded to OrderedProduct
        /// </summary>
        public static event CustomOrderVariantPriceEventHandler CustomOrderVariantPriceEvent;

        [JsonIgnore]
        public int Id
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("id"));
            }
        }
        [JsonIgnore]
        public Guid Key
        {
            get
            {
                var key = Properties.GetPropertyValue("key");

                var _key = new Guid();

                if (!Guid.TryParse(key, out _key))
                {
                    throw new Exception("No key present for product.");
                }

                return _key;
            }
        }
        [JsonIgnore]
        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }
        [JsonIgnore]
        public string Title
        {
            get
            {
                return Properties.GetPropertyValue("title", storeInfo.Alias);
            }
        }
        [JsonIgnore]
        public decimal OriginalPrice
        {
            get
            {
                var priceField = Properties.GetPropertyValue("price", storeInfo.Alias);

                decimal originalPrice = 0;
                decimal.TryParse(priceField, out originalPrice);

                return originalPrice;
            }
        }
        [JsonIgnore]
        public string Path
        {
            get
            {
                return Properties.GetPropertyValue("path");
            }
        }
        [JsonIgnore]
        public DateTime CreateDate
        {
            get
            {
                return ExamineService.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }
        [JsonIgnore]
        public DateTime UpdateDate
        {
            get
            {
                return ExamineService.ConvertToDatetime(Properties.GetPropertyValue("updateDate"));
            }
        }
        public IPrice Price
        {
            get
            {
                return new Price(OriginalPrice, storeInfo);
            }
        }
        [JsonIgnore]
        public StoreInfo StoreInfo
        {
            get
            {
                return storeInfo;
            }
        }

        public Guid[] ImageIds { get; set; }

        public IReadOnlyDictionary<string, string> Properties;

        public IEnumerable<OrderedVariantGroup> VariantGroups { get; set; }

        public OrderedProduct(Guid productId, IEnumerable<Guid> variantIds, IStore store)
        {
            var product = Catalog.Current.GetProduct(store.Alias, productId)
                as Product;

            if (product == null)
            {
                throw new Exception("OrderedProduct could not be created. Product not found. Key: " + productId);
            }

            var config = new Configuration();

            if (config.CustomOrderPrice)
            {
                // This gives null error if not fired
                CustomOrderProductPriceEvent(new CustomOrderProductPriceEventArgs
                {
                    Product = product
                });
            }

            storeInfo = new StoreInfo(store);

            ImageIds = product.Images.Any() ? product.Images.Select(x => x.Key).ToArray() : new Guid[] { };

            Properties = product.Properties;

            if (variantIds.Any())
            {
                var variantGroups = new List<OrderedVariantGroup>();

                foreach (var variantId in variantIds)
                {
                    var variant = Catalog.Current.GetVariant(store.Alias, variantId)
                        as Variant;

                    if (variant == null)
                    {
                        throw new Exception("OrderedProduct could not be created. Variant not found. Key: " + variantId);
                    }

                    if (config.CustomOrderPrice)
                    {
                        // This gives null error if not fired
                        CustomOrderVariantPriceEvent(new CustomOrderVariantPriceEventArgs
                        {
                            Variant = variant
                        });

                    }

                    var variantGroup = variant.VariantGroup;

                    variantGroups.Add(new OrderedVariantGroup(variant, variantGroup, store));
                }

                VariantGroups = variantGroups;
            }
            else
            {
                VariantGroups = Enumerable.Empty<OrderedVariantGroup>();
            }
        }

        public OrderedProduct(string productJson, StoreInfo storeInfo)
        {
            this.productJson = productJson;
            this.storeInfo = storeInfo;

            Log.Info("Created OrderedProduct from json");

            var productPropertiesObject = JObject.Parse(productJson);

            Properties = productPropertiesObject["Properties"].ToObject<Dictionary<string, string>>();

            ImageIds = productPropertiesObject["ImageIds"].ToObject<Guid[]>();

            // Add Variant Group

            var variantGroups = productPropertiesObject["VariantGroups"];

            var variantsGroupList = new List<OrderedVariantGroup>();

            if (variantGroups != null && !string.IsNullOrEmpty(variantGroups.ToString()))
            {
                Log.Info("OrderedProduct: Variant Groups found in Json");

                var variantGroupsArray = (JArray)variantGroups;

                if (variantGroupsArray != null && variantGroupsArray.Any())
                {
                    Log.Info("OrderedProduct: Variant Groups items found in array json");

                    foreach (var variantGroupObject in variantGroupsArray)
                    {
                        var variantGroup = new OrderedVariantGroup(variantGroupObject, storeInfo);

                        variantsGroupList.Add(variantGroup);
                    }
                }
            }

            if (variantsGroupList.Any())
            {
                VariantGroups = variantsGroupList;
            }
            else
            {
                VariantGroups = Enumerable.Empty<OrderedVariantGroup>();
            }
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

    }

    public class CustomOrderProductPriceEventArgs : EventArgs
    {
        public IProduct Product { get; set; }
    }

    public class CustomOrderVariantPriceEventArgs : EventArgs
    {
        public IVariant Variant { get; set; }
    }
}
