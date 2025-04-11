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
    private IPerStoreCache<ICategory> _categoryCache => Configuration.Resolver.GetService<IPerStoreCache<ICategory>>();
    private IPerStoreCache<IProduct> _productCache => Configuration.Resolver.GetService<IPerStoreCache<IProduct>>();
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
    public ICategory RootCategory
    {
        get
        {
            return Ancestors().FirstOrDefault();
        }
    }

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
    {
        get
        {
            IOrderedEnumerable<ICategory> subs = _categoryCache.Cache[Store.Alias]
                .Where(x => x.Value.ParentId == Id)
                .Select(x => x.Value)
                .OrderBy(x => x.SortOrder);


            return subs;
        }
    }

    /// <summary>
    /// All descendant categories, includes grandchild categories
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public IEnumerable<ICategory> SubCategoriesRecursive
    {
        get
        {
            return _categoryCache.Cache[Store.Alias]
                                .Where(x => x.Value.Level > Level &&
                                            x.Value.PathArray.Contains(Id.ToString()))
                                .Select(x => x.Value)
                                .OrderBy(x => x.SortOrder);
        }
    }

    public virtual bool VirtualUrl
    {
        get
        {
            string virtualUrl = Properties.GetPropertyValue("ekmVirtualUrl");

            if (!string.IsNullOrEmpty(virtualUrl))
            {
                return virtualUrl.IsBoolean();
            }

            return false;
        }
    }

    public virtual bool HasProducts()
    {
        return _productCache.Cache[Store.Alias]
                            .Any(x => x.Value.Categories.Any(z => z.Id == Id));
    }

    /// <summary>
    /// All direct child products of category. (No descendants)
    /// </summary>
    public ProductResponse Products(ProductQuery? query = null)
    {

        IEnumerable<IProduct> products = _productCache.Cache[Store.Alias]
                            .Where(x => x.Value.Categories.Any(z => z.Id == Id))
                            .Select(x => x.Value).AsEnumerable();

        return new ProductResponse(products, query, _productFilterService, this);
    }


    /// <summary>
    /// All descendant products of category, this includes child products of sub-categories
    /// </summary>
    public ProductResponse ProductsRecursive(ProductQuery? query = null)
    {
        List<ICategory> categories = _categoryCache.Cache[Store.Alias]
            .Where(x => x.Value.Level >= Level &&
                        x.Value.PathArray.Contains(Id.ToString()))
            .Select(x => x.Value)
            .ToList(); // ToList for better performance in the next query

        HashSet<int> categoryIds = new HashSet<int>(categories.Select(c => c.Id));

        IEnumerable<IProduct> products = _productCache.Cache[Store.Alias]
            .Where(x => x.Value.Categories != null && x.Value.Categories.Any(cat => categoryIds.Contains(cat.Id)))
            .Select(x => x.Value)
            .AsEnumerable();

        return new ProductResponse(products, query, _productFilterService, this);
    }

    /// <summary>
    /// All parent categories, grandparent categories and so on.
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ICategory> Ancestors()
    {
        List<ICategory> list = new List<ICategory>();

        foreach (string id in PathArray)
        {
            if (int.TryParse(id, out int categoryId))
            {
                KeyValuePair<Guid, ICategory> category = _categoryCache.Cache[Store.Alias]
                    .FirstOrDefault(x => x.Value.Id == categoryId);

                if (category.Value != null && !category.Value.VirtualUrl)
                {
                    list.Add(category.Value);
                }
            }
        }

        return list;
    }


    public IEnumerable<MetafieldGrouped> Filters(bool filterable = true)
    {
        return ProductsRecursive().Products.Filters();
    }

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

        IEnumerable<UmbracoContent> ancestors = nodeSvc.GetAllCatalogAncestors(item);

        List<UmbracoUrl> urls = urlSvc.BuildCategoryUrls(ancestors, store);

        UrlsWithContext = urls;
        Urls = urls.Select(x => x.Url);
    }
}
