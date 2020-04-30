using Ekom.Interfaces;
using Ekom.JsonDotNet;
using Ekom.Services;
using Ekom.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Web;

namespace Ekom.Models.OrderedObjects
{
    public class OrderedProduct
    {
        private readonly string _productJson;

        public int Id
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("__NodeId"));
            }
        }

        public Guid Key
        {
            get
            {
                var key = Properties.GetPropertyValue("__Key");

                var _key = new Guid();

                if (!Guid.TryParse(key, out _key))
                {
                    throw new Exception("No key present for product.");
                }

                return _key;
            }
        }

        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }
        public OrderedProductDiscount ProductDiscount { get; }

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
                return Properties.GetPropertyValue("__Path");
            }
        }
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        //TODO Store default setup!
        public bool Backorder => Properties.GetPropertyValue("enableBackorder", StoreInfo.Alias).IsBoolean();

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
                if (!Configuration.Current.DisableCartImages)
                {
                    var umbHelper = Current.Factory.GetInstance<UmbracoHelper>();
                    return ImageIds.Select(id => umbHelper.Media(id)?.Url);
                }
                return null;
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
            ImageIds = product.Images.Any() 
                ? product.Images.Select(x => x.Key).ToArray() 
                : new Guid[] { };

            Properties = new ReadOnlyDictionary<string, string>(
                product.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            Price = product.Price.Clone() as IPrice;
            Discount = product.Discount == null ? null : new OrderedDiscount(product.Discount);
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

            var logger = Current.Factory.GetInstance<ILogger>();
            logger.Debug<OrderedProduct>("Created OrderedProduct from json");

            var productPropertiesObject = JObject.Parse(productJson);
            Discount = productPropertiesObject[nameof(Discount)] != null 
                ? productPropertiesObject[nameof(Discount)].ToObject<OrderedDiscount>(EkomJsonDotNet.serializer) 
                : null;
            Properties = new ReadOnlyDictionary<string, string>(
                productPropertiesObject[nameof(Properties)].ToObject<Dictionary<string, string>>());
            logger.Debug<OrderedProduct>("OrderedProductPriceJson: " + productPropertiesObject[nameof(Price)]);
            try
            {
                Price = productPropertiesObject[nameof(Price)].ToObject<Price>(EkomJsonDotNet.serializer);
            }
            catch (Exception)
            {
                // failed due to old order, try to fix by splitting the constructor up.
                Price = new Price(productPropertiesObject[nameof(Price)]);
            }

            ImageIds = productPropertiesObject[nameof(ImageIds)].ToObject<Guid[]>();

            // Add Variant Group

            var variantGroups = productPropertiesObject[nameof(VariantGroups)];

            var variantsGroupList = new List<OrderedVariantGroup>();

            if (variantGroups != null && !string.IsNullOrEmpty(variantGroups.ToString()))
            {
                logger.Debug<OrderedProduct>("OrderedProduct: Variant Groups found in Json");

                var variantGroupsArray = (JArray)variantGroups;

                if (variantGroupsArray != null && variantGroupsArray.Any())
                {
                    logger.Debug<OrderedProduct>("OrderedProduct: Variant Groups items found in array json");

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
    }
}
