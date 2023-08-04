using Ekom.API;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Xml.Serialization;


namespace Ekom.Models
{
    /// <summary>
    /// A customization of a parent product, currently must belong to a <see cref="Models.VariantGroup"/>
    /// Price of variant is added to product base price to calculate total price.
    /// Has seperate stock from base product.
    /// </summary>
    public class Variant : PerStoreNodeEntity, IVariant, IPerStoreNodeEntity
    {
        /// <summary>
        /// Stock Keeping Unit, identifier
        /// </summary>
        public string SKU => string.IsNullOrEmpty(Properties.GetPropertyValue("sku")) ? Product.SKU : Properties.GetPropertyValue("sku");

        /// <summary>
        /// Get the variant stock
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public virtual int Stock => API.Stock.Instance.GetStock(Key);

        /// <summary>
        /// Get the backorder status
        /// </summary>
        public virtual bool Backorder
        {
            get
            {
                //TODO Store default setup!

                var backOrderValue = Properties.GetPropertyValue("enableBackorder", Store.Alias);

                return !string.IsNullOrEmpty(backOrderValue) && backOrderValue.IsBoolean();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual string Description => Properties.GetPropertyValue("description", Store.Alias);

        /// <summary>
        /// Get the availability of the variant
        /// </summary>
        public virtual bool Available => Stock > 0 || Backorder;

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

                int id = Convert.ToInt32(paths[paths.Length - 3]);

                return id;
            }
        }

        public int VariantGroupId
        {
            get
            {
                return ParentId;
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
            return Configuration.Resolver.GetService<ProductDiscountService>()
                .GetProductDiscount(
                    Path,
                    Store.Alias,
                    price,
                    Product.Categories.Select(x => x.Id.ToString()).ToArray()
                );
        }

        /// <summary>
        /// Variant group <see cref="IVariant"/> belongs to
        /// </summary>
        [JsonIgnore]
        [XmlIgnore]
        public IVariantGroup VariantGroup
        {
            get
            {
                return Catalog.Instance.GetVariantGroup(Store.Alias, VariantGroupId);
            }
        }

        /// <summary>
        /// Get Price by current store currency
        /// </summary>
        public IPrice Price
        {
            get => CookieHelper.GetCurrencyPriceCookieValue(Prices, Store.Alias);
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

                foreach (var p in prices.Where(x => x.OriginalValue == 0).ToList())
                {
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
                if (Properties.HasPropertyValue("vat", Store.Alias))
                {
                    return Convert.ToDecimal(Properties.GetPropertyValue("vat", Store.Alias)) / 100;
                }

                return Product.Vat;
            }
        }

        // <summary>
        // Variant images
        // </summary>
        public virtual IEnumerable<Image> Images
        {
            get
            {
                var _images = Properties.GetPropertyValue(Configuration.Instance.CustomImage);

                var imageNodes = _images.GetImages();

                return imageNodes;
            }
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
        /// Construct Variant from UmbracoContent
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public Variant(UmbracoContent item, IStore store) : base(item, store)
        {

        }
    }
}
