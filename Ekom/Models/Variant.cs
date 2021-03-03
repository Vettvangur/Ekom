using Ekom.API;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models.OrderedObjects;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// A customization of a parent product, currently must belong to a <see cref="Models.VariantGroup"/>
    /// Price of variant is added to product base price to calculate total price.
    /// Has seperate stock from base product.
    /// </summary>
    public class Variant : PerStoreNodeEntity, IVariant, IPerStoreNodeEntity
    {
        private IPerStoreCache<IVariantGroup> __variantGroupCache;

        //private IPerStoreCache<IVariantGroup> _variantGroupCache =>
        //    __variantGroupCache ?? (__variantGroupCache = Current.Factory.GetInstance<IPerStoreCache<IVariantGroup>>());

        /// <summary>
        /// Stock Keeping Unit, identifier
        /// </summary>
        public string SKU => string.IsNullOrEmpty(Properties.GetPropertyValue("sku")) ? Product.SKU : Properties.GetPropertyValue("sku");

        /// <summary>
        /// 
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public virtual int Stock => API.Stock.Instance.GetStock(Key);

        /// <summary>
        /// Parent <see cref="IProduct"/> of Variant
        /// </summary>
        public IProduct Product
        {
            get
            {
                var product = Catalog.Instance.GetProduct(Store.Alias, ProductId);

                if (product == null)
                {
                    throw new KeyNotFoundException("Variant Product not found. Key: " + ProductId);
                }

                return product;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public int ProductId
        {
            get
            {
                var paths = Path.Split(',');

                int productId = Convert.ToInt32(paths[paths.Length - 3]);

                return productId;
            }
        }

        /// <summary>
        /// Get the Product Key
        /// </summary>
        public Guid ProductKey
        {
            get
            {
                return Product.Key;
            }
        }

        /// <summary>
        /// Gets the productDiscount for the specific Variant
        /// </summary>
        public IProductDiscount ProductDiscount(string price)
        {
            return Current.Factory.GetInstance<IProductDiscountService>()
                .GetProductDiscount(
                    Path,
                    Store.Alias,
                    price,
                    Product.Categories.Select(x => x.Id.ToString()).ToArray()
                );
        }

        // Waiting for variants to be composed with their parent product
        ///// <summary>
        ///// Get the Product Key
        ///// </summary>
        //public Guid ProductKey => Product.Key;
        ///// <summary>
        ///// 
        ///// </summary>
        //public int ProductId => Product.Id;

        /// <summary>
        /// <see cref="IVariantGroup"/> Key
        /// </summary>
        public Guid VariantGroupKey { get; set; }

        /// <summary>
        /// Variant group <see cref="IVariant"/> belongs to
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public IVariantGroup VariantGroup
        {
            get
            {
                var parentId = Properties.GetPropertyValue("parentID");

                if (int.TryParse(parentId, out int _parentId))
                {

                    return Catalog.Instance.GetVariantGroup(Store.Alias, VariantGroupKey);
                }

                return null;
            }
        }

        /// <summary>
        /// Get Price by current store currency
        /// </summary>
        public IPrice Price
        {
            get
            {
                var prices = Prices.ToList();

                if (HttpContext.Current != null)
                {
                    var cookie = HttpContext.Current.Request.Cookies["EkomCurrency-" + Store.Alias];

                    if (cookie != null && !string.IsNullOrEmpty(cookie.Value))
                    {
                        var price = prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie.Value);

                        if (price != null)
                        {
                            return price;
                        }
                    }

                }

                return prices.FirstOrDefault();

            }
        }

        public virtual List<IPrice> Prices
        {
            get
            {
                var prices = Properties.GetPropertyValue("price", Store.Alias)
                    .GetPriceValues(
                        Store.Currencies,
                        Vat,
                        Store.VatIncludedInPrice,
                        Store.Currency,
                        Store.Alias,
                        Path,
                        Product.Categories.Select(x => x.Id.ToString()).ToArray()
                        );

                foreach (var p in prices.Where(x => x.OriginalValue == 0).ToList()) {
                    var index = prices.IndexOf(p);

                    prices[index] = Product.Prices.FirstOrDefault(x => x.Currency.CurrencyValue == p.Currency.CurrencyValue);

                }
                return prices;
            }
        }

        public virtual decimal Vat
        {
            get
            {
                return Product.Vat;
            }
        }

        // <summary>
        // Variant images
        // </summary>
        public virtual IEnumerable<Image> Images()
        {
            var _images = Properties.GetPropertyValue(Configuration.Current.CustomImage, Store.Alias);

            var imageNodes = _images.GetImages();

            return imageNodes;
        }

        /// <summary>
        /// All categories variant belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        public List<ICategory> Categories()
        {
            var paths = Path.Split(',');

            int categoryId = Convert.ToInt32(paths[paths.Length - 4]);

            var categoryField = Properties.Any(x => x.Key == "categories") ?
                                Properties.GetPropertyValue("categories") : "";

            var categories = new List<ICategory>();

            var primaryCategory = API.Catalog.Instance.GetCategory(Store.Alias, categoryId);

            if (primaryCategory != null)
            {
                categories.Add(primaryCategory);
            }

            if (!string.IsNullOrEmpty(categoryField))
            {
                var categoryIds = categoryField.Split(',');

                foreach (var catId in categoryIds)
                {
                    var intCatId = Convert.ToInt32(catId);

                    var categoryItem
                        = Catalog.Instance.GetCategory(Store.Alias, intCatId);

                    if (categoryItem != null && !categories.Contains(categoryItem))
                    {
                        categories.Add(categoryItem);
                    }
                }
            }

            return categories;
        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public Variant(IStore store) : base(store) { }

        /// <summary>
        /// Construct Variant from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public Variant(ISearchResult item, IStore store) : base(item, store)
        {
            var examineNode = Current.Factory.GetInstance<IExamineService>().GetExamineNode(ParentId);

            if (examineNode != null)
            {
                if (Guid.TryParse(examineNode["__Key"], out Guid key))
                {
                    VariantGroupKey = key;
                }
            }
        }

        /// <summary>
        /// Construct Variant from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public Variant(IContent node, IStore store) : base(node, store)
        {
            var parentNode = Current.Services.ContentService.GetById(node.ParentId);

            VariantGroupKey = parentNode.Key;
        }
    }
}
