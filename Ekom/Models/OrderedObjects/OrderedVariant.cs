using Ekom.Interfaces;
using Ekom.JsonDotNet;
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
using System.Web.Script.Serialization;
using System.Xml.Serialization;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedVariant
    {
        private JToken variantObject;
        private StoreInfo storeInfo;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public int Id
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("id"));
            }
        }
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
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
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public string Title
        {
            get
            {
                return Properties.GetPropertyValue("title", storeInfo.Alias);
            }
        }
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public string Path
        {
            get
            {
                return Properties.GetPropertyValue("path");
            }
        }
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public DateTime CreateDate
        {
            get
            {
                return ExamineService.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
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
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public StoreInfo StoreInfo
        {
            get
            {
                return storeInfo;
            }
        }

        public IReadOnlyDictionary<string, string> Properties;

        /// <summary>
        /// ctor
        /// </summary>
        public OrderedVariant(IVariant variant, StoreInfo storeInfo)
        {
            variant = variant ?? throw new ArgumentNullException(nameof(variant));
            this.storeInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));

            Price = variant.Price.Clone() as IPrice;

            Properties = new ReadOnlyDictionary<string, string>(
                variant.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        /// <summary>
        /// Json Constructor
        /// </summary>
        public OrderedVariant(JToken variantObject, StoreInfo storeInfo)
        {
            this.variantObject = variantObject;
            this.storeInfo = storeInfo;
            Price = variantObject["Price"].ToObject<Price>(EkomJsonDotNet.serializer);

            Properties = new ReadOnlyDictionary<string, string>(
                variantObject["Properties"].ToObject<Dictionary<string, string>>());
        }

        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

    }
}
