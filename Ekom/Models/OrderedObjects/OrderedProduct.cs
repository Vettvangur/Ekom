using Ekom.API;
using Ekom.Interfaces;
using Ekom.Services;
using Ekom.Utilities;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Umbraco.Web;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedProduct
    {
        private string productJson;
        private StoreInfo storeInfo;

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

        public IPrice Price { get; }

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
            var product = Catalog.Current.GetProduct(store.Alias, productId);

            if (product == null)
            {
                throw new Exception("OrderedProduct could not be created. Product not found. Key: " + productId);
            }

            storeInfo = new StoreInfo(store);

            ImageIds = product.Images.Any() ? product.Images.Select(x => x.Key).ToArray() : new Guid[] { };

            Properties = new ReadOnlyDictionary<string, string>(
                product.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            Price = product.Price.Clone() as IPrice;

            if (variantIds.Any())
            {
                var variantGroups = new List<OrderedVariantGroup>();

                foreach (var variantId in variantIds)
                {
                    var variant = Catalog.Current.GetVariant(store.Alias, variantId);

                    if (variant == null)
                    {
                        throw new Exception("OrderedProduct could not be created. Variant not found. Key: " + variantId);
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

            Properties = new ReadOnlyDictionary<string, string>(
                productPropertiesObject["Properties"].ToObject<Dictionary<string, string>>());
            Price = productPropertiesObject["Price"].ToObject<Price>();

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
}
