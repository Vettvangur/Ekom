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
using Umbraco.Web;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedProduct
    {
        private string _productJson;

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
                return Properties.GetPropertyValue("title", StoreInfo.Alias);
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

        public IPrice Price { get; }

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public StoreInfo StoreInfo { get; }

        /// <summary>
        /// Umbraco media uniqueid's
        /// </summary>
        public Guid[] ImageIds { get; set; }

        /// <summary>
        /// Uses <see cref="UmbracoHelper"/> to attempt to get Urls for all <see cref="ImageIds"/>
        /// </summary>
        public IEnumerable<string> ImageUrls
        {
            get
            {
                var umbHelper = Configuration.container.GetInstance<UmbracoHelper>();
                return ImageIds.Select(id => umbHelper.TypedMedia(id)?.Url);
            }
        }

        public IReadOnlyDictionary<string, string> Properties;

        public IEnumerable<OrderedVariantGroup> VariantGroups { get; set; }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderedProduct(IProduct product, IVariant variant, StoreInfo storeInfo)
        {
            product = product ?? throw new ArgumentNullException(nameof(product));
            StoreInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));
            ImageIds = product.Images.Any() ? product.Images.Select(x => x.Key).ToArray() : new Guid[] { };

            Properties = new ReadOnlyDictionary<string, string>(
                product.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            Price = product.Price.Clone() as IPrice;

            if (variant != null)
            {
                var variantGroups = new List<OrderedVariantGroup>();

                var variantGroup = variant.VariantGroup;


                variantGroups.Add(new OrderedVariantGroup(variant, variantGroup, storeInfo));

                VariantGroups = variantGroups;
            }
            else
            {
                VariantGroups = Enumerable.Empty<OrderedVariantGroup>();
            }
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderedProduct(string productJson, StoreInfo storeInfo)
        {
            _productJson = productJson;
            StoreInfo = storeInfo;

            Log.Debug("Created OrderedProduct from json");

            var productPropertiesObject = JObject.Parse(productJson);

            Properties = new ReadOnlyDictionary<string, string>(
                productPropertiesObject["Properties"].ToObject<Dictionary<string, string>>());
            Price = productPropertiesObject["Price"].ToObject<Price>(EkomJsonDotNet.serializer);

            ImageIds = productPropertiesObject["ImageIds"].ToObject<Guid[]>();

            // Add Variant Group

            var variantGroups = productPropertiesObject["VariantGroups"];

            var variantsGroupList = new List<OrderedVariantGroup>();

            if (variantGroups != null && !string.IsNullOrEmpty(variantGroups.ToString()))
            {
                Log.Debug("OrderedProduct: Variant Groups found in Json");

                var variantGroupsArray = (JArray)variantGroups;

                if (variantGroupsArray != null && variantGroupsArray.Any())
                {
                    Log.Debug("OrderedProduct: Variant Groups items found in array json");

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

        /// <summary>
        /// 
        /// </summary>
        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
