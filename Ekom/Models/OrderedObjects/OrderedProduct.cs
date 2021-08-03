using Ekom.Interfaces;
using Ekom.JsonDotNet;
using Ekom.Services;
using Ekom.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
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
        public IDiscount ProductDiscount { get; }

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
        public bool Backorder => Properties.GetPropertyValue("enableBackorder", StoreInfo.Alias).IsBooleanTrue();

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

        public IPrice Price
        {
            get
            {
                return Prices.FirstOrDefault(x => x.Currency.CurrencyValue == StoreInfo.Currency.CurrencyValue);
            }
        }

        public List<IPrice> Prices { get; }

        public decimal Vat
        {
            get
            {
                if (Properties.HasPropertyValue("vat", StoreInfo.Alias))
                {
                    return Convert.ToDecimal(Properties.GetPropertyValue("vat", StoreInfo.Alias)) / 100;
                }

                return StoreInfo.Vat;
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        private StoreInfo StoreInfo { get; }

        public IReadOnlyDictionary<string, string> Properties;

        public IEnumerable<OrderedVariantGroup> VariantGroups { get; set; }

        // <summary>
        // Product images
        // </summary>
        public virtual IEnumerable<Image> Images()
        {
            var value = ConfigurationManager.AppSettings["ekmCustomImage"];

            var _images = Properties.GetPropertyValue(value ?? "images", StoreInfo.Alias);

            var imageNodes = _images.GetImages();

            return imageNodes;
        }

        /// <summary>
        /// ctor
        /// </summary>
        public OrderedProduct(IProduct product, IVariant variant, StoreInfo storeInfo)
        {
            product = product ?? throw new ArgumentNullException(nameof(product));
            StoreInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));

            Properties = new ReadOnlyDictionary<string, string>(
               product.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

            //Price = product.Price.Clone() as IPrice;
            Prices = product.Prices.ToList();

            var productDiscount = product.ProductDiscount(Price.Value.ToString());

            ProductDiscount = productDiscount != null ? new OrderedDiscount(productDiscount) : null;

            if (variant != null)
            {
                var variantGroups = new List<OrderedVariantGroup>();

                var variantGroup = variant.VariantGroup;


                variantGroups.Add(new OrderedVariantGroup(variant, variantGroup, storeInfo, Vat));

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
            StoreInfo = storeInfo;

            var logger = Current.Factory.GetInstance<ILogger>();

            logger.Debug<OrderedProduct>("Created OrderedProduct from json");

            var productPropertiesObject = JObject.Parse(productJson);
            ProductDiscount = productPropertiesObject[nameof(ProductDiscount)]?
                .ToObject<IDiscount>(EkomJsonDotNet.serializer);

            Properties = new ReadOnlyDictionary<string, string>(
                productPropertiesObject[nameof(Properties)].ToObject<Dictionary<string, string>>());

            var pricesObj = productPropertiesObject[nameof(Prices)];

            var priceObj = productPropertiesObject[nameof(Price)];

            try
            {
                if (pricesObj != null && !string.IsNullOrEmpty(pricesObj.ToString()))
                {

                    Prices = pricesObj.ToString().GetPriceValuesConstructed(Vat, storeInfo.VatIncludedInPrice, storeInfo.Currency);
                }
                else
                {
                    try
                    {
                        Prices = new List<IPrice>()
                        {
                            priceObj.ToObject<Price>(EkomJsonDotNet.serializer)
                        };
                    }
                    catch
                    {
                        Prices = new List<IPrice>()
                        {
                            new Price(priceObj, storeInfo.Currency, storeInfo.Vat, storeInfo.VatIncludedInPrice)
                        };
                    }
                }

            }
            catch (Exception ex)
            {
                logger.Error<OrderedProduct>(ex,"Failed to construct price. ID: " + Id + " Price Object: " + (priceObj != null ? priceObj.ToString() : "Null") + " Prices Object: " + (pricesObj != null ? pricesObj.ToString() : "Null"));
            }

            //Price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == StoreInfo.Currency.CurrencyValue);

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
