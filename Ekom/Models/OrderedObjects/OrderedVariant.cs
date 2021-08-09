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

namespace Ekom.Models.OrderedObjects
{
    public class OrderedVariant
    {
        private readonly JToken variantObject;

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public int Id
        {
            get
            {
                return Convert.ToInt32(Properties.GetPropertyValue("__NodeId"));
            }
        }
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
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
                return Properties.GetPropertyValue("__Path");
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
        public IPrice Price
        {
            get
            {
                return Prices.FirstOrDefault(x => x.Currency.CurrencyValue == StoreInfo.Currency.CurrencyValue);
            }
            set { }
        }

        public List<IPrice> Prices { get; }

        public decimal ProductVat { get; set; }
        public decimal Vat 
        {
            get
            {
                if (Properties.HasPropertyValue("vat", StoreInfo.Alias))
                {
                    return Convert.ToDecimal(Properties.GetPropertyValue("vat", StoreInfo.Alias)) / 100;
                }

                return ProductVat;
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        private StoreInfo StoreInfo { get; }

        public IReadOnlyDictionary<string, string> Properties;

        // <summary>
        // Variant images
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
        public OrderedVariant(IVariant variant, StoreInfo storeInfo, decimal productVat)
        {
            variant = variant ?? throw new ArgumentNullException(nameof(variant));
            StoreInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));

            ProductVat = productVat;
            Prices = variant.Prices.ToList();

            Properties = new ReadOnlyDictionary<string, string>(
                variant.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
        }

        /// <summary>
        /// Json Constructor
        /// </summary>
        public OrderedVariant(JToken variantObject, StoreInfo storeInfo)
        {
            this.variantObject = variantObject;
            StoreInfo = storeInfo;

            ProductVat = (decimal)(variantObject["ProductVat"] ?? 0);
            var pricesObj = variantObject["Prices"];
            var priceObj = variantObject["Price"];

            Properties = new ReadOnlyDictionary<string, string>(
                variantObject[nameof(Properties)].ToObject<Dictionary<string, string>>());

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
                //Log.Error("Failed to construct price. ID: " + Id + " Price Object: " + (priceObj != null ? priceObj.ToString() : "Null") + " Prices Object: " + (pricesObj != null ? pricesObj.ToString() : "Null"), ex);
            }

            Properties = new ReadOnlyDictionary<string, string>(
                variantObject[nameof(Properties)].ToObject<Dictionary<string, string>>());
        }
    }
}
