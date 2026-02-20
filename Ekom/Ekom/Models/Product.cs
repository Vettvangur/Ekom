using Ekom.API;
using Ekom.Cache;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Globalization;
using System.Xml.Serialization;

namespace Ekom.Models;

/// <summary>
/// An Ekom store product
/// </summary>
public class Product : PerStoreNodeEntity, IProduct
{
    private IPerStoreIndexedCache<IVariant> __variantCache;
    private IPerStoreIndexedCache<IVariant> _variantCache =>
        __variantCache ?? (__variantCache = Configuration.Resolver.GetService<IPerStoreIndexedCache<IVariant>>());

    private IPerStoreIndexedCache<IVariantGroup> __variantGroupCache;
    private IPerStoreIndexedCache<IVariantGroup> _variantGroupCache =>
        __variantGroupCache ?? (__variantGroupCache = Configuration.Resolver.GetService<IPerStoreIndexedCache<IVariantGroup>>());

    private readonly ConcurrentDictionary<string, Lazy<object>> _cache = new();

    public virtual IDiscount ProductDiscount(string? price = null)
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
    public string SKU {get; set;}

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
    /// Get the current product Stock
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual decimal? StockBuffer { get; set; }

    /// <summary>
    /// Get the availability of the product and the variants
    /// </summary>
    public virtual bool Available => Stock > 0 || Backorder || AllVariants.Any(x => x.Available);

    private string _backorderValue = "";
    /// <summary>
    /// Get the backorder status
    /// </summary>
    public virtual bool Backorder
    {
        get
        {
            return !string.IsNullOrEmpty(_backorderValue) && _backorderValue.IsBoolean();
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
            var primaryVariantGroup = PrimaryVariantGroup;

            var lazy = _cache.GetOrAdd("PrimaryVariant", _ =>
                new Lazy<object?>(() =>
                {
                    if (primaryVariantGroup == null)
                        return null;

                    var variants = primaryVariantGroup.Variants.ToList();

                    var primaryVariant = variants.FirstOrDefault(v => v.Available)
                                        ?? variants.FirstOrDefault();

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
    public IPrice Price => CookieHelper.GetCurrencyPriceCookieValue(Prices, Store.Alias) ?? new Price(0, Store.Currency, Store.Vat, Store.VatIncludedInPrice);

    private string _priceValue = "";

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
            var storeCurrency = Store.Currency;

            string productKey = Path;

            string globalGen = PriceCache.GlobalGeneration;
            string productGen = PriceCache.GetItemGeneration(productKey);

            var key = $"prices:g={globalGen}:p={productGen}:store={Store.Alias}:prod={productKey}:cats={string.Join('|', categories)}:hash={CacheHelpers.Sha256(_priceValue)}";

            return CacheHelpers.GetOrCreateSingleFlight(
                key,
                () => PriceBuilder.BuildPricesSync(
                    _priceValue,
                    Store.Currencies,
                    Vat,
                    vatIncludedInPrice,
                    storeCurrency,
                    Store.Alias,
                    Path,
                    categories),
                TimeSpan.FromHours(48)
            );
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public string RawPriceValue => _priceValue;

    public List<IPrice> BuildPricesFromRaw(
        string[]? categories = null)
    {
        categories ??= Categories.Select(x => x.Id.ToString()).ToArray();

        return PriceBuilder.BuildPricesSync(
            _priceValue,
            Store.Currencies,
            Vat,
            Store.VatIncludedInPrice,
            Store.Currency,
            Store.Alias,
            Path,
            categories
        );
    }


    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public virtual IPrice OriginalPrice { get; set; }


    public virtual decimal Vat
    {
        get
        {
            var storeAlias = Store.Alias;
            var storeVat = Store.Vat;

            var lazy = _cache.GetOrAdd($"Vat", _ =>
                new Lazy<object>(() =>
                {
                    if (Properties.HasPropertyValue("vat", storeAlias))
                    {
                        string value = GetValue("vat", storeAlias);
                        if (!string.IsNullOrEmpty(value) && decimal.TryParse(value, out decimal _val))
                        {
                            return _val / 100;
                        }
                    }

                    return storeVat;
                }, LazyThreadSafetyMode.ExecutionAndPublication)
            );

            return (decimal)((Lazy<object>)lazy).Value;
        }
    }


    private List<MetavalueSlim>? _metafieldsCache;

    public virtual List<MetavalueSlim> Metafields
    {
        get
        {
            if (_metafieldsCache is not null) return _metafieldsCache;

            if (!Properties.HasPropertyValue("metafields"))
                return _metafieldsCache = [];

            var value = GetValue("metafields");
            var metafieldService = Configuration.Resolver.GetService<IMetafieldService>();
            if (metafieldService is null)
                return _metafieldsCache = [];

            var metafields = metafieldService.SerializeMetafields(value, Id);

            _metafieldsCache = metafields.Select(m => new MetavalueSlim
            {
                Field = new MetafieldSlim
                {
                    Id = m.Field.Id,
                    Key = m.Field.Key,
                    Name = m.Field.Name,
                    Alias = m.Field.Alias,
                    Title = m.Field.Title,
                    Description = m.Field.Description,
                    Filterable = m.Field.Filterable,
                    EnableMultipleChoice = m.Field.EnableMultipleChoice,
                    Required = m.Field.Required,
                    ReadOnly = m.Field.ReadOnly,
                    AllConditionsMustMatch = m.Field.AllConditionsMustMatch,
                    Values = m.Field.Values
                },
                Values = m.Values
            }).ToList();

            return _metafieldsCache;
        }
    }

    /// <summary>
    /// All child variant groups of this product
    /// </summary>
    public virtual IEnumerable<IVariantGroup> VariantGroups
    {
        get
        {
            var storeAlias = Store.Alias;
            var productId = Id;

            var vg = _variantGroupCache as Ekom.Cache.VariantGroupCache
                ?? throw new InvalidOperationException("Expected _variantGroupCache to be VariantGroupCache (product index required).");

            // materialize once
            return vg.GetByProductId(storeAlias, productId).ToList();
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
            var storeAlias = Store.Alias;
            var productId = Id;

            var lazy = _cache.GetOrAdd($"AllVariants_{storeAlias}_{productId}", _ =>
                new Lazy<object>(() =>
                {
                    var vc = _variantCache as Ekom.Cache.VariantCache
                        ?? throw new InvalidOperationException("Expected _variantCache to be VariantCache (product index required).");

                    // Materialize once so callers don't re-enumerate
                    return vc.GetByProductId(storeAlias, productId).ToList();
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

       var urls = Configuration.Resolver.GetService<IUrlService>()?.BuildProductUrlsWithContext(item, Categories, store, item.Id) ?? new List<UmbracoUrl>();

        UrlsWithContext = urls;
        Urls = urls.Select(x => x.Url).ToList();

        _priceValue = GetValue("price", Store.Alias) ?? string.Empty;
        _backorderValue = GetValue("enableBackorder", Store.Alias);

        OriginalPrice = CreateOriginalPrice();
        SKU = GetValue("sku");

        if (decimal.TryParse(GetValue("ekmStockBuffer", store.Alias), out var stockBuffer))
        {
            StockBuffer = stockBuffer;
        }
    }

    public void InvalidateCache()
    {
        PriceCache.InvalidateItem(Path);
        _cache.Clear();
    }

    private void PopulateCategories()
    {
        // Dedupe by category Id (cheap, stable)
        var seen = new HashSet<int>();

        void AddCategory(ICategory? cat)
        {
            if (cat == null) return;
            if (seen.Add(cat.Id))
                categories.Add(cat);
        }

        // Primary category from ParentId
        AddCategory(Catalog.Instance.GetCategory(ParentId, Store.Alias));

        // Extra categories from "categories" field
        if (!Properties.ContainsKey("categories"))
            return;

        var categoryField = GetValue("categories");
        if (string.IsNullOrWhiteSpace(categoryField))
            return;

        ReadOnlySpan<char> span = categoryField.AsSpan();
        int pos = 0;

        while (pos < span.Length)
        {
            // skip separators/whitespace
            while (pos < span.Length && (span[pos] == ',' || char.IsWhiteSpace(span[pos])))
                pos++;

            if (pos >= span.Length)
                break;

            int start = pos;

            // read until comma
            while (pos < span.Length && span[pos] != ',')
                pos++;

            var token = span.Slice(start, pos - start).Trim();

            if (token.Length == 0)
                continue;

            // Try int first (fast path)
            if (int.TryParse(token, out var id))
            {
                AddCategory(Catalog.Instance.GetCategory(id, Store.Alias));
            }
            else
            {
                // Fallback: maybe guid/udi/string format used elsewhere
                AddCategory(Catalog.Instance.GetCategory(token.ToString(), Store.Alias));
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
        return RelatedProductsAsync(count).GetAwaiter().GetResult();
    }

    public async Task<IReadOnlyList<IProduct>> RelatedProductsAsync(int count = 4, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (count <= 0) return Array.Empty<IProduct>();

        // Prevent duplicates (in case related + category overlap)
        var seen = new HashSet<Guid>();
        var result = new List<IProduct>(capacity: count);

        // Always exclude current product
        seen.Add(Key);

        // 1) Related products field
        if (Properties.HasPropertyValue("relatedProducts"))
        {
            var val = GetValue("relatedProducts");
            if (!string.IsNullOrWhiteSpace(val))
            {
                UtilityService.ConvertUdisToGuids(val, out IEnumerable<Guid> guids);

                foreach (var guid in guids)
                {
                    ct.ThrowIfCancellationRequested();
                    if (result.Count >= count) break;
                    if (!seen.Add(guid)) continue;

                    // async fetch so async product events can run
                    var p = await Catalog.Instance.GetProductAsync(guid, Store.Alias, raiseEvent: true, ct: ct);
                    if (p is null) continue;

                    if (p.Key == Key || p.Id == Id) continue;

                    result.Add(p);
                }
            }
        }

        // 2) Fill from category (recursive)
        if (result.Count < count)
        {
            ct.ThrowIfCancellationRequested();

            // async fetch so async category events can run
            var category = await Catalog.Instance.GetCategoryAsync(ParentId, Store.Alias, global: false, raiseEvent: true, ct: ct);

            if (category != null)
            {
                var needed = count - result.Count;

                // async recursive so ProductResponse pipeline is async-first
                var pr = await category.ProductsRecursiveAsync(query: null, ct: ct);

                foreach (var p in pr.Products)
                {
                    ct.ThrowIfCancellationRequested();
                    if (needed == 0) break;
                    if (p is null) continue;

                    // Skip current + duplicates
                    if (p.Id == Id) continue;
                    if (!seen.Add(p.Key)) continue;

                    result.Add(p);
                    needed--;
                }
            }
        }

        return result;
    }

    private IPrice CreateOriginalPrice()
    {
        var storeCurrency = Store.Currency;

        var vat = Vat;
        if (vat <= 0) vat = Store.Vat;

        var storeVatIncluded = Store.VatIncludedInPrice;

        var s = _priceValue;
        if (s.TryGetOriginalPriceValue(Store?.Alias, out var value))
            return new Price(value, storeCurrency, vat, storeVatIncluded);

        return new Price(0, storeCurrency, vat, storeVatIncluded);
    }

}
