using Ekom.API;
using Ekom.Cache;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Xml.Serialization;

namespace Ekom.Models;

/// <summary>
/// An Ekom store product
/// </summary>
public class Product : PerStoreNodeEntity, IProduct
{
    private IPerStoreCache<IVariant> __variantCache;
    private IPerStoreCache<IVariant> _variantCache =>
        __variantCache ?? (__variantCache = Configuration.Resolver.GetService<IPerStoreCache<IVariant>>());

    private IPerStoreCache<IVariantGroup> __variantGroupCache;
    private IPerStoreCache<IVariantGroup> _variantGroupCache =>
        __variantGroupCache ?? (__variantGroupCache = Configuration.Resolver.GetService<IPerStoreCache<IVariantGroup>>());

    private readonly ConcurrentDictionary<string, Lazy<object>> _cache = new();

    public virtual IDiscount ProductDiscount(string price = null)
    {
        price = string.IsNullOrEmpty(price) ? Price.OriginalValue.ToString() : price;

        return Configuration.Resolver.GetService<ProductDiscountService>()?
                .GetProductDiscount(
                    Path,
                    Store.Alias,
                    price,
                    categories.Select(x => x.Id.ToString())?.ToArray()
                );
    }

    /// <summary>
    /// Product SKU
    /// </summary>
    public string SKU => GetValue("sku");

    /// <summary>
    /// 
    /// </summary>
    public virtual string Description => GetValue("description", Store.Alias);

    /// <summary>
    /// 
    /// </summary>
    public virtual string Summary => GetValue("summary", Store.Alias);

    /// <summary>
    /// Short spaceless descriptive title used to create URLs
    /// </summary>
    public string Slug => GetValue("slug", Store.Alias);

    /// <summary>
    /// Get the current product Stock
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual decimal Stock => API.Stock.Instance.GetStock(Key);

    /// <summary>
    /// Get the availability of the product and the variants
    /// </summary>
    public virtual bool Available => Stock > 0 || Backorder || AllVariants.Any(x => x.Available);

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

    // <summary>
    // Product images
    // </summary>
    public virtual IEnumerable<Image> Images
    {
        get
        {
            IVariantGroup? primaryVariantGroup = PrimaryVariantGroup;

            if (primaryVariantGroup != null)
            {
                List<Image>? imageNodes = primaryVariantGroup.Images.ToList();

                if (!imageNodes.Any() && primaryVariantGroup.Variants.Any())
                {
                    imageNodes = primaryVariantGroup.Variants.FirstOrDefault()?.Images.ToList();
                }

                if (imageNodes != null && imageNodes.Any())
                {
                    return imageNodes;
                }
            }

            var lazy = _cache.GetOrAdd("Images", _ =>
                new Lazy<object>(() =>
                {
                    string _images = GetValue(Configuration.Instance.CustomImage);
                    return _images.GetImages().ToList(); // Ensure immediate evaluation
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (IEnumerable<Image>)((Lazy<object>)lazy).Value;
        }
    }


    /// <summary>
    /// A product can have multiple variant groups, 
    /// therefore we allow to configure a default/primary variant group.
    /// If none is configured, we return the first possible item.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IVariantGroup? PrimaryVariantGroup
    {
        get
        {
            List<IVariantGroup> variantGroups = VariantGroups.ToList();

            if (!variantGroups.Any())
            {
                return null;
            }

            var lazy = _cache.GetOrAdd("PrimaryVariantGroup", _ =>
                new Lazy<object>(() =>
                {
                    if (Properties.ContainsKey("primaryVariantGroup"))
                    {
                        string primaryGroupValue = GetValue("primaryVariantGroup");

                        if (!string.IsNullOrEmpty(primaryGroupValue))
                        {
                            UmbracoContent? node = Configuration.Resolver.GetService<INodeService>()?.NodeById(primaryGroupValue);

                            if (node != null && node.ContentTypeAlias == "ekmProductVariantGroup")
                            {
                                if (__variantGroupCache.Cache.TryGetValue(Store.Alias, out var storeDict) && storeDict.TryGetValue(node.Key, out var variantGroup))
                                {
                                    return variantGroup;
                                }
                            }
                        }
                    }

                    return variantGroups.FirstOrDefault(x => x.Available) ?? variantGroups.FirstOrDefault();
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (IVariantGroup?)((Lazy<object?>)lazy).Value;
        }
    }


    /// <summary>
    /// Select the Primary variant.
    /// First Variant in the primary variant group that is available, if none are available, return the first variant.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IVariant? PrimaryVariant
    {
        get
        {
            var lazy = _cache.GetOrAdd("PrimaryVariant", _ =>
                new Lazy<object?>(() =>
                {
                    IVariantGroup? primaryVariantGroup = PrimaryVariantGroup;

                    if (primaryVariantGroup == null)
                    {
                        return null;
                    }

                    // Try to find the first available variant, or fall back to any variant
                    IVariant? primaryVariant = primaryVariantGroup.Variants.FirstOrDefault(v => v.Available)
                                            ?? primaryVariantGroup.Variants.FirstOrDefault();

                    return primaryVariant;
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (IVariant?)((Lazy<object?>)lazy).Value;
        }
    }


    /// <summary>
    /// All ancestor categories this <see cref="Product"/> belongs to from the primary category.
    /// </summary>
    /// <returns></returns>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IEnumerable<ICategory> CategoryAncestors => categoryAncestors.AsReadOnly();
    internal List<ICategory> categoryAncestors = new();

    /// <summary>
    /// All categories product belongs to, includes parent category and related categories.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IEnumerable<ICategory> Categories => categories.AsReadOnly();
    internal List<ICategory> categories = new();

    /// <summary>
    /// All ID's of categories product belongs to, includes parent category and related categories.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IEnumerable<Guid> CategoriesIds
    {
        get
        {
            return Categories.Select(x => x.Key);
        }
    }

    /// <inheritdoc/>
    public virtual string Url => Configuration.Resolver.GetService<IUrlService>()?.GetNodeEntityUrl(this) ?? "";

    /// <summary>
    /// All product urls, computed from stores and categories.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IEnumerable<string> Urls { get; internal set; }

    /// <summary>
    /// All product urls with context, computed from stores and categories.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual List<UmbracoUrl> UrlsWithContext { get; internal set; }

    /// <summary>
    /// Get Price by current store currency
    /// </summary>
    public IPrice Price => GetPrice();

    private IPrice GetPrice()
    {
        HttpContext? httpCtx = Configuration.Resolver.GetService<IHttpContextAccessor>().HttpContext;
        string? cookie = httpCtx?.Request.Cookies["EkomCurrency-" + Store.Alias];

        if (cookie != null && !string.IsNullOrEmpty(cookie))
        {
            IPrice? price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == cookie);

            if (price != null)
            {
                return price;
            }
        }

        System.Globalization.CultureInfo? culture = httpCtx?.Request.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture;

        if (culture != null)
        {
            IPrice? price = Prices.FirstOrDefault(x => x.Currency.CurrencyValue == culture.Name);

            if (price != null)
            {
                return price;
            }
        }

        return Prices.FirstOrDefault();
    }

    /// <summary>
    /// 
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual List<IPrice> Prices
    {
        get
        {
            string[] categories = Categories.Select(x => x.Id.ToString()).ToArray();

            bool vatIncludedInPrice = Store.VatIncludedInPrice;
            CurrencyModel storeCurrency = Store.Currency;

            List<IPrice> prices = GetValue("price", Store.Alias)
                .GetPriceValues(
                    Store.Currencies,
                    Vat,
                    vatIncludedInPrice,
                    storeCurrency,
                    Store.Alias,
                    Path,
                    categories
                );

            return prices;
        }
    }


    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IPrice OriginalPrice
    {
        get
        {
            var lazy = _cache.GetOrAdd("OriginalPrice", _ =>
                new Lazy<object>(() =>
                {
                    if (PrimaryVariant != null)
                    {
                        var primaryVariantOriginalPrice = PrimaryVariant.OriginalPrice;

                        if (primaryVariantOriginalPrice.Value > 0)
                        {
                            return primaryVariantOriginalPrice;
                        }
                    }

                    // Store frequently accessed values to avoid redundant access
                    CurrencyModel storeCurrency = Store.Currency;
                    decimal storeVat = Store.Vat;
                    bool storeVatIncluded = Store.VatIncludedInPrice;

                    string originalPrice = GetValue("price", Store.Alias);

                    if (string.IsNullOrEmpty(originalPrice))
                    {
                        return new Price(0, storeCurrency, storeVat, storeVatIncluded);
                    }

                    if (decimal.TryParse(originalPrice, out decimal _orgPrice))
                    {
                        return new Price(_orgPrice, storeCurrency, storeVat, storeVatIncluded);
                    }

                    if (originalPrice.IsJson())
                    {
                        var orgPrice = JsonConvert.DeserializeObject<List<CurrencyPrice>>(originalPrice);
                        decimal? val = orgPrice?.FirstOrDefault()?.Price;

                        if (val.HasValue)
                        {
                            return new Price(val.Value, storeCurrency, storeVat, storeVatIncluded);
                        }
                    }

                    // If no price is found, return a price of 0 with store settings
                    return new Price(0, storeCurrency, storeVat, storeVatIncluded);
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (IPrice)((Lazy<object>)lazy).Value;
        }
    }


    public virtual decimal Vat
    {
        get
        {
            var lazy = _cache.GetOrAdd("Vat", _ =>
                new Lazy<object>(() =>
                {
                    if (Properties.HasPropertyValue("vat", Store.Alias))
                    {
                        string value = GetValue("vat", Store.Alias);
                        if (!string.IsNullOrEmpty(value) && decimal.TryParse(value, out decimal _val))
                        {
                            return _val / 100;
                        }
                    }

                    return Store.Vat;
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (decimal)((Lazy<object>)lazy).Value;
        }
    }


    public virtual List<Metavalue> Metafields
    {
        get
        {
            if (Properties.HasPropertyValue("metafields"))
            {
                string value = GetValue("metafields");

                return Configuration.Resolver.GetService<IMetafieldService>()?.SerializeMetafields(value, Id) ?? new List<Metavalue>();
            }

            return new List<Metavalue>();
        }
    }

    /// <summary>
    /// All child variant groups of this product
    /// </summary>
    public virtual IEnumerable<IVariantGroup> VariantGroups
    {
        get
        {
            var lazy = _cache.GetOrAdd("VariantGroups", _ =>
                new Lazy<object>(() =>
                    (from pair in _variantGroupCache.Cache[Store.Alias]
                     let variantGroup = pair.Value
                     where variantGroup.ProductId == Id
                     orderby variantGroup.SortOrder
                     select variantGroup).ToList(),
                LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (IEnumerable<IVariantGroup>)lazy.Value;
        }
    }

    /// <summary>
    /// All variants belonging to product.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IEnumerable<IVariant> AllVariants
    {
        get
        {
            var lazy = _cache.GetOrAdd("AllVariants", _ =>
                new Lazy<object>(() =>
                {
                    return (from pair in _variantCache.Cache[Store.Alias]
                            let variant = pair.Value
                            where variant.ProductId == Id
                            select variant).ToList(); // Evaluate immediately to avoid re-enumeration
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (IEnumerable<IVariant>)((Lazy<object>)lazy).Value;
        }
    }


    /// <summary>
    /// Get Variant Count
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public int AllVariantsCount
    {
        get
        {
            return AllVariants.Count();
        }

    }

    /// <summary>
    /// Used by Ekom extensions, keep logic empty to allow full customisation of object construction.
    /// </summary>
    /// <param name="store"></param>
    internal protected Product(IStore store) : base(store) { }

    /// <summary>
    /// Construct Product
    /// </summary>
    /// <param name="item"></param>
    /// <param name="store"></param>
    internal protected Product(UmbracoContent item, IStore store) : base(item, store)
    {
        PopulateCategoryAncestors();
        PopulateCategories();

        List<UmbracoUrl> urls = Configuration.Resolver.GetService<IUrlService>().BuildProductUrlsWithContext(item, Categories, store, item.Id);

        UrlsWithContext = urls;
        Urls = urls.Select(x => x.Url).ToList();
        InvalidateCache();
    }

    public void InvalidateCache()
    {
        _cache.Clear();
    }

    private void PopulateCategories()
    {
        int categoryId = ParentId;

        string categoryField = Properties.ContainsKey("categories") ?
                            GetValue("categories") : "";

        ICategory? primaryCategory = Catalog.Instance.GetCategory(categoryId, Store.Alias);

        if (primaryCategory != null)
        {
            categories.Add(primaryCategory);
        }

        if (!string.IsNullOrEmpty(categoryField))
        {
            string[] categoryIds = categoryField.Split(',');

            foreach (string catId in categoryIds)
            {
                ICategory categoryItem
                    = Catalog.Instance.GetCategory(catId, Store.Alias);

                if (categoryItem != null && !categories.Contains(categoryItem))
                {
                    categories.Add(categoryItem);
                }
            }
        }
    }

    private void PopulateCategoryAncestors()
    {

        foreach (string? p in PathArray.Skip(2))
        {
            if (int.TryParse(p, out int id))
            {
                ICategory? c = Catalog.Instance.GetCategory(id, Store.Alias);

                if (c != null && !c.VirtualUrl)
                {
                    categoryAncestors.Add(c);
                }
            }
        }

        categoryAncestors.Reverse();
    }

    public IEnumerable<IProduct> RelatedProducts(int count = 4)
    {
        List<IProduct> relatedProducts = new List<IProduct>();

        if (Properties.HasPropertyValue("relatedProducts"))
        {
            string val = GetValue("relatedProducts");

            if (!string.IsNullOrEmpty(val))
            {
                UtilityService.ConvertUdisToGuids(val, out IEnumerable<Guid> guids);

                foreach (Guid id in guids.Where(x => x != Key).Take(count))
                {
                    IProduct? product = Catalog.Instance.GetProduct(id, Store.Alias);

                    if (product != null && product.Key != Key)
                    {
                        relatedProducts.Add(product);
                    }
                }
            }
        }

        if (!relatedProducts.Any() || relatedProducts.Count < 4)
        {
            ICategory? category = Catalog.Instance.GetCategory(ParentId);

            if (category != null)
            {
                relatedProducts.AddRange(category.ProductsRecursive().Products.Where(x => x.Id != Id).Take(count - relatedProducts.Count).ToList());
            }

        }

        return relatedProducts;
    }
}
