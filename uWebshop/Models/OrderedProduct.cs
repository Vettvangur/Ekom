using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Helpers;
using uWebshop.Interfaces;

namespace uWebshop.Models
{
    public class OrderedProduct
    {
        private string productJson;
        private string variantsJson;
        private StoreInfo storeInfo;

        //public IProduct Product { get; set; }
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

        public IEnumerable<OrderedVariant> Variants {get; set;}

        public OrderedProduct(Guid productId, IEnumerable<Guid> variantIds, Store store)
        {
            var product = API.Catalog.GetProduct(productId);
            storeInfo = new StoreInfo(store);

            Properties = product.Properties;

            if (variantIds.Any())
            {
                var variants = new List<OrderedVariant>();

                foreach (var variantId in variantIds)
                {
                    variants.Add(new OrderedVariant(productId, variantId));
                }

                Variants = variants;
            }

        }

        public OrderedProduct(string productJson, string variantsJson, StoreInfo storeInfo)
        {
            this.productJson = productJson;
            this.variantsJson = variantsJson;
            this.storeInfo = storeInfo;

            var productPropertiesObject = JObject.Parse(productJson);

            var productProperties = (JArray)productPropertiesObject["Properties"];

            var properties = new List<UmbracoProperty>();

            foreach (var property in productProperties)
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
