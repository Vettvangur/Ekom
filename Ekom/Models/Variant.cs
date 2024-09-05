using Ekom.API;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Xml.Serialization;


namespace Ekom.Models;

/// <summary>
/// A customization of a parent product, currently must belong to a <see cref="Models.VariantGroup"/>
/// Price of variant is added to product base price to calculate total price.
/// Has seperate stock from base product.
/// </summary>
public class Variant : PerStoreNodeEntity, IVariant, IPerStoreNodeEntity
{
    private readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();

    /// <summary>
    /// Stock Keeping Unit, identifier
    /// </summary>
    public string SKU => string.IsNullOrEmpty(GetValue("sku")) ? Product.SKU : GetValue("sku");

    /// <summary>
    /// Get the variant stock
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
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

            var backOrderValue = GetValue("enableBackorder", Store.Alias);

            return !string.IsNullOrEmpty(backOrderValue) && backOrderValue.IsBoolean();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual string Description => GetValue("description", Store.Alias);

    /// <summary>
    /// Get the availability of the variant
    /// </summary>
    public virtual bool Available => Stock > 0 || Backorder;

    /// <summary>
    /// Parent <see cref="IProduct"/> of Variant
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IProduct Product
    {
        get
        {
            var product = Catalog.Instance.GetProduct(ProductId, Store.Alias);

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
            int id = Convert.ToInt32(PathArray[PathArray.Length - 3]);

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
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IVariantGroup VariantGroup
    {
        get
        {
            return Catalog.Instance.GetVariantGroup(ParentKey, Store.Alias);
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IPrice OriginalPrice
    {
        get
        {
            return (IPrice)_cache.GetOrAdd("OriginalPrice", key =>
            {
                var originalPrice = GetValue("price", Store.Alias);

                if (string.IsNullOrEmpty(originalPrice))
                {
                    return new Price(0, Store.Currency, Store.Vat, Store.VatIncludedInPrice);
                }

                if (decimal.TryParse(originalPrice, out decimal _orgPrice))
                {
                    return new Price(_orgPrice, Store.Currency, Store.Vat, Store.VatIncludedInPrice);
                }

                if (originalPrice.IsJson())
                {
                    var orgPrice = JsonConvert.DeserializeObject<List<CurrencyPrice>>(originalPrice);
                    var val = orgPrice?.FirstOrDefault()?.Price;

                    if (val.HasValue)
                    {
                        return new Price(val.Value, Store.Currency, Store.Vat, Store.VatIncludedInPrice);
                    }
                }

                // If no price is found, return a price of 0 with store settings
                return new Price(0, Store.Currency, Store.Vat, Store.VatIncludedInPrice);
            });

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
                return Convert.ToDecimal(GetValue("vat", Store.Alias)) / 100;
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
            return (IEnumerable<Image>)_cache.GetOrAdd("Images", key =>
            {
                var _images = GetValue(Configuration.Instance.CustomImage);

                var imageNodes = _images.GetImages();

                return imageNodes;
            });
        }
    }

    /// <summary>
    /// All categories variant belongs to, includes parent category.
    /// Does not include categories product is an indirect child of.
    /// </summary>
    public virtual List<ICategory> Categories()
    {
        int categoryId = Convert.ToInt32(PathArray[PathArray.Length - 4]);

        var categoryField = Properties.Any(x => x.Key == "categories") ?
                            GetValue("categories") : "";

        var categories = new List<ICategory>();

        var primaryCategory = API.Catalog.Instance.GetCategory(categoryId, Store.Alias);

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
                    = Catalog.Instance.GetCategory(intCatId, Store.Alias);

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
        Product.InvalidateCache();
        InvalidateCache();
    }

    public void InvalidateCache()
    {
        _cache.Clear();
    }
}
