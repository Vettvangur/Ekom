using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Ekom.API;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Services;
using Ekom.Utilities;
using log4net;
using System.Reflection;

namespace Ekom.Models
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

        public OrderedVariant(Guid variantId, Store store)
        {
            var variant = Catalog.Current.GetVariant(store.Alias, variantId);
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

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

    }
}
