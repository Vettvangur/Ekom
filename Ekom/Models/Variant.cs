using Ekom.API;
using Ekom.Cache;
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
    public string SKU { get; set; }

    /// <summary>
    /// Get the variant stock
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual decimal Stock => API.Stock.Instance.GetStock(Key);

    /// <summary>
    /// Get the backorder status
    /// </summary>
    public virtual bool Backorder
    {
        get
        {
            //TODO Store default setup!

            string backOrderValue = GetValue("enableBackorder", Store.Alias);

            return !string.IsNullOrEmpty(backOrderValue) && backOrderValue.IsBoolean();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual string Description { get; set; }

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
    public virtual IProduct? Product
    {
        get
        {
            return Catalog.Instance.GetProduct(ProductId, Store.Alias);
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
            var cacheKey = $"ProductKey_{Id}";

            var lazy = (Lazy<object>)_cache.GetOrAdd(cacheKey, _ =>
                new Lazy<object>(() =>
                {
                    // Only fetch the key — not the full Product
                    return Product?.Key ?? Guid.Empty;
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (Guid)lazy.Value;
        }
    }

    /// <summary>
    /// Gets the productDiscount for the specific Variant
    /// </summary>
    public IProductDiscount? ProductDiscount(string price)
    {
        return Configuration.Resolver.GetService<ProductDiscountService>()?
            .GetProductDiscount(
                Path,
                Store.Alias,
                price,
                Product?.Categories.Select(x => x.Id.ToString()).ToArray()
            );
    }

    /// <summary>
    /// Variant group <see cref="IVariant"/> belongs to
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IVariantGroup? VariantGroup
    {
        get
        {
            return Catalog.Instance.GetVariantGroup(ParentKey, Store.Alias);
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IPrice OriginalPrice { get; set; }

    private string _priceValue = "";

    /// <summary>
    /// Get Price by current store currency
    /// </summary>
    public IPrice Price => CookieHelper.GetCurrencyPriceCookieValue(Prices, Store.Alias);

    public virtual List<IPrice> Prices
    {
        get
        {
            string[] categories = Product?.Categories.Select(x => x.Id.ToString()).ToArray()
                                    ?? Array.Empty<string>();

            string itemKey = Path;

            string globalGen = PriceCache.GlobalGeneration;
            string productGen = PriceCache.GetItemGeneration(itemKey);

            string cacheKey =
                $"prices:g={globalGen}:p={productGen}:store={Store.Alias}:prod={itemKey}:cats={string.Join('|', categories)}:hash={CacheHelpers.Sha256(_priceValue)}";

            return CacheHelpers.GetOrCreateSingleFlight(
                cacheKey,
                () =>
                {

                    List<IPrice> prices = PriceBuilder.BuildPricesSync(
                        _priceValue,
                        Store.Currencies,
                        Vat,
                        Store.VatIncludedInPrice,
                        Store.Currency,
                        Store.Alias,
                        Path,
                        categories
                    );

                    // --- Repair any zero-priced entries using Product.Prices ---
                    if (Product != null)
                    {
                        var fallbackPrices = Product.Prices;

                        foreach (IPrice? p in prices.Where(x => x.OriginalValue == 0).ToList())
                        {
                            var replacement = fallbackPrices
                                .FirstOrDefault(x => x.Currency.CurrencyValue == p.Currency.CurrencyValue);

                            if (replacement != null)
                            {
                                int index = prices.IndexOf(p);
                                if (index >= 0)
                                {
                                    prices[index] = replacement;
                                }
                            }
                        }
                    }

                    return prices;
                },
                TimeSpan.FromHours(48)
            );
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

            return Product?.Vat ?? 0;
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
                string _images = GetValue(Configuration.Instance.CustomImage);

                IEnumerable<Image> imageNodes = _images.GetImages();

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

        string categoryField = Properties.Any(x => x.Key == "categories") ?
                            GetValue("categories") : "";

        var categories = new List<ICategory>();

        ICategory? primaryCategory = API.Catalog.Instance.GetCategory(categoryId, Store.Alias);

        if (primaryCategory != null)
        {
            categories.Add(primaryCategory);
        }

        if (!string.IsNullOrEmpty(categoryField))
        {
            string[] categoryIds = categoryField.Split(',');

            foreach (string catId in categoryIds)
            {
                int intCatId = Convert.ToInt32(catId);

                ICategory? categoryItem
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
        Product?.InvalidateCache();
        PriceCache.InvalidateItem(Path);

        _priceValue = GetValue("price", Store.Alias) ?? string.Empty;
        OriginalPrice = CreateOriginalPrice();

        SKU = string.IsNullOrEmpty(GetValue("sku")) ? Product?.SKU ?? "" : GetValue("sku");
        Description = GetValue("description", Store.Alias);
    }

    private IPrice CreateOriginalPrice()
    {
        string originalPrice = _priceValue;

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
            List<CurrencyPrice>? orgPrice = JsonConvert.DeserializeObject<List<CurrencyPrice>>(originalPrice);
            decimal? val = orgPrice?.FirstOrDefault()?.Price;

            if (val.HasValue)
            {
                return new Price(val.Value, Store.Currency, Store.Vat, Store.VatIncludedInPrice);
            }
        }

        // If no price is found, return a price of 0 with store settings
        return new Price(0, Store.Currency, Store.Vat, Store.VatIncludedInPrice);
    }
}
