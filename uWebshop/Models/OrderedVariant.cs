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
            propertyAlias = propertyAlias.ToLowerInvariant();

            if (!string.IsNullOrEmpty(propertyAlias))
            {
                if (Properties.ContainsKey(propertyAlias.ToLowerInvariant()))
                {
                    return Properties[propertyAlias.ToLowerInvariant()];
                }
            }

            return null;
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

            Properties = variantObject["Properties"].ToObject<Dictionary<string, string>>();
        }
    }
}
