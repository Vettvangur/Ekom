using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Helpers;
using uWebshop.Interfaces;

namespace uWebshop.Models
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
                return Convert.ToInt32(GetPropertyValue("id"));
            }
        }
        [JsonIgnore]
        public Guid Key
        {
            get
            {
                var key = GetPropertyValue("key");

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
                return GetPropertyValue("sku");
            }
        }
        [JsonIgnore]
        public string Title
        {
            get
            {
                return Properties.GetStoreProperty("title", storeInfo.Alias);
            }
        }
        [JsonIgnore]
        public decimal OriginalPrice
        {
            get
            {
                var priceField = Properties.GetStoreProperty("price", storeInfo.Alias);

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
                return GetPropertyValue("path");
            }
        }
        [JsonIgnore]
        public DateTime CreateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue("createDate"));
            }
        }
        [JsonIgnore]
        public DateTime UpdateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue("updateDate"));
            }
        }
        public IDiscountedPrice Price
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

        public Dictionary<string, string> Properties = new Dictionary<string, string>();

        public string GetPropertyValue(string propertyAlias)
        {
            if (!string.IsNullOrEmpty(propertyAlias))
            {
                if (Properties.ContainsKey(propertyAlias))
                {
                    return Properties[propertyAlias];
                }
            }

            return null;
        }

        public IEnumerable<OrderedVariantGroup> VariantGroups { get; set; }

        public OrderedProduct(Guid productId, IEnumerable<Guid> variantIds, Store store)
        {
            var product = API.Catalog.GetProduct(store.Alias, productId);

            if (product == null)
            {
                throw new Exception("OrderedProduct could not be created. Product not found. Key: " + productId);
            }

            storeInfo = new StoreInfo(store);

            Properties = product.Properties;

            if (variantIds.Any())
            {
                var variantGroups = new List<OrderedVariantGroup>();

                foreach (var variantId in variantIds)
                {
                    var variant = API.Catalog.GetVariant(store.Alias, variantId);

                    if (variant == null)
                    {
                        throw new Exception("OrderedProduct could not be created. Variant not found. Key: " + variantId);
                    }

                    var variantGroup = variant.VariantGroup();

                    variantGroups.Add(new OrderedVariantGroup(variant, variantGroup, store));
                }

                VariantGroups = variantGroups;
            } else
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
