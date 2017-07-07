using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using uWebshop.Interfaces;
using Newtonsoft.Json;
using uWebshop.Helpers;

namespace uWebshop.Models
{
    public class OrderedVariant
    {
        private JToken variantObject;
        private StoreInfo storeInfo;

        [JsonIgnore]
        public int Id
        {
            get
            {
                return Convert.ToInt32(GetPropertyValue<string>("id"));
            }
        }
        [JsonIgnore]
        public Guid Key
        {
            get
            {
                var key = GetPropertyValue<string>("key");

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
                return GetPropertyValue<string>("sku");
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
                return GetPropertyValue<string>("path");
            }
        }
        [JsonIgnore]
        public DateTime CreateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue<string>("createDate"));
            }
        }
        [JsonIgnore]
        public DateTime UpdateDate
        {
            get
            {
                return ExamineHelper.ConvertToDatetime(GetPropertyValue<string>("updateDate"));
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

        public List<UmbracoProperty> Properties = new List<UmbracoProperty>();

        public T GetPropertyValue<T>(string propertyAlias)
        {
            propertyAlias = propertyAlias.ToLowerInvariant();

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                if (Properties.Any(x => x.Key.ToLowerInvariant() == propertyAlias))
                {
                    var property = Properties.FirstOrDefault(x => x.Key.ToLowerInvariant() == propertyAlias);

                    return property == null ? default(T) : (T)property.Value;
                }

            }

            return default(T);
        }
        public OrderedVariant(Guid variantId, Store store)
        {
            var variant = API.Catalog.GetVariant(store.Alias,variantId);
            storeInfo = new StoreInfo(store);

            Properties = variant.Properties;
        }

        public OrderedVariant(Variant variant, Store store)
        {
            storeInfo = new StoreInfo(store);

            Properties = variant.Properties;
        }

        public OrderedVariant(JToken variantObject, StoreInfo storeInfo)
        {
            this.variantObject = variantObject;
            this.storeInfo = storeInfo;

            var variantProperties = (JArray)variantObject["Properties"];

            if (variantProperties != null)
            {
                var properties = new List<UmbracoProperty>();

                foreach (var property in variantProperties)
                {
                    properties.Add(new UmbracoProperty
                    {
                        Key = (string)property["Key"],
                        Value = (string)property["Value"]
                    });
                }

                Properties = properties;
            }


        }
    }
}
