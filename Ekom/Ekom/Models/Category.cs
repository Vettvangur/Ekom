using Ekom.Cache;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System.Xml.Serialization;


namespace Ekom.Models;

/// <summary>
/// Categories are groupings of products, categories can also be nested, f.x.
/// Women->Winter->Shirts
/// </summary>
public class Category : PerStoreNodeEntity, ICategory
{
    private IPerStoreIndexedCache<ICategory> _categoryCache => Configuration.Resolver.GetService<IPerStoreIndexedCache<ICategory>>();
    private IPerStoreIndexedCache<IProduct> _productCache => Configuration.Resolver.GetService<IPerStoreIndexedCache<IProduct>>();
    private IProductFilterService _productFilterService => Configuration.Resolver.GetService<IProductFilterService>();
    /// <summary>
    /// Short spaceless descriptive title used to create URLs
    /// </summary>
    public string Slug => Properties.GetPropertyValue("slug", base.Store.Alias);

    /// <summary>
    /// All category Urls, computed from stores
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IEnumerable<string> Urls { get; set; }

    /// <summary>
    /// All category Urls with context, computed from stores
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public List<UmbracoUrl> UrlsWithContext { get; set; }

    /// <summary>
    /// Our eldest ancestor category
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public ICategory? RootCategory { get; set; }

    /// <inheritdoc/>
    public virtual string Url
    {
        get
        {
            IUrlService? urlSvc = Configuration.Resolver.GetService<IUrlService>();
            return urlSvc?.GetNodeEntityUrl(this) ?? "";
        }
    }

    /// <summary>
    /// All direct child categories
    /// </summary>
    public IEnumerable<ICategory> SubCategories
        => ((CategoryCache)_categoryCache).GetChildren(Store.Alias, Id);

    /// <summary>
    /// All descendant categories, includes grandchild categories
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IEnumerable<ICategory> SubCategoriesRecursive
    => ((CategoryCache)_categoryCache).GetDescendants(Store.Alias, Id)
       .Where(c => c.Level > Level);

    public virtual bool VirtualUrl { get; set; }

    public virtual bool HasProducts()
    {
        var storeAlias = Store.Alias;

        var productCache = _productCache as ProductCache
            ?? throw new InvalidOperationException("Expected _productCache to be ProductCache (category index required).");

        return productCache.HasAnyInCategory(storeAlias, Id);
    }


    /// <summary>
    /// All direct child products of category. (No descendants)
    /// </summary>
    public ProductResponse Products(ProductQuery? query = null)
    {
        var storeAlias = Store.Alias;

        var productCache = _productCache as ProductCache
            ?? throw new InvalidOperationException("Expected _productCache to be ProductCache (category index required).");

        var products = productCache.GetByAnyCategoryIds(storeAlias, [Id]);

        return new ProductResponse(products, query, _productFilterService, this);
    }


    /// <summary>
    /// All direct child products of category. (No descendants) Async
    /// </summary>
    public ValueTask<ProductResponse> ProductsAsync(ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var storeAlias = Store.Alias;

        var productCache = _productCache as ProductCache
            ?? throw new InvalidOperationException("Expected _productCache to be ProductCache (category index required).");

        var products = productCache.GetByAnyCategoryIds(storeAlias, new[] { Id });

        ct.ThrowIfCancellationRequested();

        // ValueTask-returning pipeline already exists via CreateAsync (Task),
        // so we wrap it as ValueTask
        return new ValueTask<ProductResponse>(
            ProductResponse.CreateAsync(products, query, _productFilterService, category: this, ct: ct));
    }


    /// <summary>
    /// All descendant products of category, this includes child products of sub-categories
    /// </summary>
    public ProductResponse ProductsRecursive(ProductQuery? query = null)
    {
        var storeAlias = Store.Alias;

        var categoryIds = new HashSet<int>();
        var idStr = Id.ToString();

        if (_categoryCache.Cache.TryGetValue(storeAlias, out var catDict))
        {
            foreach (var c in catDict.Values)
            {
                if (c.Level >= Level && c.PathArray.Contains(idStr))
                    categoryIds.Add(c.Id);
            }
        }

        if (categoryIds.Count == 0)
            return new ProductResponse(Enumerable.Empty<IProduct>(), query, _productFilterService, this);

        var productCache = _productCache as ProductCache
            ?? throw new InvalidOperationException("Expected _productCache to be ProductCache (category index required).");

        var products = productCache.GetByAnyCategoryIds(storeAlias, categoryIds);

        return new ProductResponse(products, query, _productFilterService, this);
    }

    /// <summary>
    /// All descendant products of category, this includes child products of sub-categories (async).
    /// </summary>
    public async Task<ProductResponse> ProductsRecursiveAsync(ProductQuery? query = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var storeAlias = Store.Alias;

        var categoryIds = new HashSet<int>();
        var idStr = Id.ToString();

        if (_categoryCache.Cache.TryGetValue(storeAlias, out var catDict))
        {
            foreach (var c in catDict.Values)
            {
                ct.ThrowIfCancellationRequested();

                if (c.Level >= Level && c.PathArray.Contains(idStr))
                    categoryIds.Add(c.Id);
            }
        }

        if (categoryIds.Count == 0)
        {
            return await ProductResponse.CreateAsync(
                Enumerable.Empty<IProduct>(),
                query,
                _productFilterService,
                category: this,
                ct: ct);
        }

        var productCache = _productCache as ProductCache
            ?? throw new InvalidOperationException("Expected _productCache to be ProductCache (category index required).");

        ct.ThrowIfCancellationRequested();

        var products = productCache.GetByAnyCategoryIds(storeAlias, categoryIds);

        return await ProductResponse.CreateAsync(products, query, _productFilterService, category: this, ct: ct);
    }


    /// <summary>
    /// All parent categories, grandparent categories and so on.
    /// </summary>
    /// <returns></returns>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IEnumerable<ICategory> Ancestors { get; set; }

    public IEnumerable<MetafieldGrouped> Filters(bool filterable = true)
    {
        return ProductsRecursive().Products.Filters();
    }

    public async Task<IEnumerable<MetafieldGrouped>> FiltersAsync(bool filterable = true, CancellationToken ct = default)
    {
        var products = await ProductsRecursiveAsync(ct: ct);
        return products.Products.Filters();
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public decimal? StockBuffer { get; set; }


    /// <summary>
    /// Used by Ekom extensions, keep logic empty to allow full customisation of object construction.
    /// </summary>
    /// <param name="store"></param>
    internal protected Category(IStore store) : base(store) { }
    /// <summary>
    /// Construct Category from IPublishedContent item
    /// </summary>
    /// <param name="item"></param>
    /// <param name="store"></param>
    internal protected Category(UmbracoContent item, IStore store) : base(item, store)
    {
        IUrlService? urlSvc = Configuration.Resolver.GetService<IUrlService>();
        INodeService? nodeSvc = Configuration.Resolver.GetService<INodeService>();

        var ancestors = nodeSvc.GetAllCatalogAncestors(item).ToList();

        List<UmbracoUrl> urls = urlSvc.BuildCategoryUrls(ancestors, store);

        UrlsWithContext = urls;
        Urls = urls.Select(x => x.Url);

        VirtualUrl = GetValue("ekmVirtualUrl").IsBoolean();

        if (decimal.TryParse(GetValue("ekmStockBuffer", store.Alias), out var stockBuffer))
        {
            StockBuffer = stockBuffer;
        }

        var ancestorCategories = new List<ICategory>();

        foreach (var node in ancestors)
        {
            if (_categoryCache.Cache[Store.Alias].TryGetValue(node.Key, out var cat))
            {
                if (cat != null && !cat.VirtualUrl)
                {
                    ancestorCategories.Add(cat);
                }
            }
        }

        Ancestors = ancestorCategories;
        RootCategory = ancestorCategories.FirstOrDefault() ?? this;
    }
}
