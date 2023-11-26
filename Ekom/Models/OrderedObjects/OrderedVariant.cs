using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Ekom.Models
{
    public class OrderedVariant
    {
        [JsonIgnore]
        [XmlIgnore]
        public int Id
        {
            get
            {
                if (Properties.ContainsKey("__NodeId"))
                {
                    return Convert.ToInt32(Properties.GetPropertyValue("__NodeId"));
                }
                
                return Convert.ToInt32(Properties.GetPropertyValue("id"));
            }
        }
        [JsonIgnore]
        [XmlIgnore]
        public Guid Key
        {
            get
            {
                if (Properties.ContainsKey("__Key"))
                {
                    var key = Properties.GetPropertyValue("__Key");

                    if (!Guid.TryParse(key, out Guid _key))
                    {
                        throw new Exception("No key present for product.");
                    }

                    return _key;
                }

                // Backword Compatability
                return Guid.Empty;

            }
        }
        [JsonIgnore]
        [XmlIgnore]
        public string SKU
        {
            get
            {
                return Properties.GetPropertyValue("sku");
            }
        }
        [JsonIgnore]
        [XmlIgnore]
        public string Title
        {
            get
            {
                return Properties.GetPropertyValue("title", StoreInfo.Alias);
            }
        }
        [JsonIgnore]
        [XmlIgnore]
        public string Path
        {
            get
            {
                return Properties.GetPropertyValue("__Path");
            }
        }
        [JsonIgnore]
        [XmlIgnore]
        public DateTime CreateDate
        {
            get
            {
                return UtilityService.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
            }
        }
        [JsonIgnore]
        [XmlIgnore]
        public DateTime UpdateDate
        {
            get
            {
                return UtilityService.ConvertToDatetime(Properties.GetPropertyValue("updateDate"));
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
                    var value = Properties.GetPropertyValue("vat", StoreInfo.Alias);

                    if (!string.IsNullOrEmpty(value) && decimal.TryParse(value, out decimal _val))
                    {
                        return _val / 100;
                    }
                }

                return ProductVat;
            }
        }

        [JsonIgnore]
        [XmlIgnore]
        private StoreInfo StoreInfo { get; }

        public IReadOnlyDictionary<string, string> Properties;

        // <summary>
        // Variant images
        // </summary>
        public virtual IEnumerable<Image> Images()
        {
            var config = Configuration.Resolver.GetService<Configuration>();
            
            var _images = Properties.GetPropertyValue(config != null ? config.CustomImage : "images", StoreInfo.Alias);

            if (!string.IsNullOrEmpty(_images))
            {
                var imageNodes = _images.GetImages();

                return imageNodes;
            }

            return Enumerable.Empty<Image>();


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
            StoreInfo = storeInfo;

            ProductVat = (decimal)(variantObject["ProductVat"] ?? 0);
            var pricesObj = variantObject["Prices"];
            var priceObj = variantObject["Price"];

            Properties = new ReadOnlyDictionary<string, string>(
                variantObject[nameof(Properties)].ToObject<Dictionary<string, string>>());


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

            Properties = new ReadOnlyDictionary<string, string>(
                variantObject[nameof(Properties)].ToObject<Dictionary<string, string>>());
        }
    }
}
