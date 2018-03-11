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

namespace Ekom.Models.OrderedObjects
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
        /// <summary>
        /// 
        /// </summary>
        public IPrice Price { get; }
        [JsonIgnore]
        public StoreInfo StoreInfo
        {
            get
            {
                return storeInfo;
            }
        }

        public IReadOnlyDictionary<string, string> Properties;

        public OrderedVariant(Guid variantId, IStore store)
        {
            var variant = Catalog.Current.GetVariant(store.Alias, variantId);
            storeInfo = new StoreInfo(store);
            Price = variant.Price.Clone() as IPrice;

            Properties = new ReadOnlyDictionary<string, string>(
                variant.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        public OrderedVariant(IVariant variant, IStore store)
        {
            storeInfo = new StoreInfo(store);
            Price = variant.Price.Clone() as IPrice;

            Properties = new ReadOnlyDictionary<string, string>(
                variant.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        public OrderedVariant(JToken variantObject, StoreInfo storeInfo)
        {
            this.variantObject = variantObject;
            this.storeInfo = storeInfo;
            Price = variantObject["Price"].ToObject<Price>();

            Properties = new ReadOnlyDictionary<string, string>(
                variantObject["Properties"].ToObject<Dictionary<string, string>>());
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

    }
}
