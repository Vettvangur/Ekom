using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers;

[Route("ekom/backoffice/catalog-listview")]
[CamelCaseJson]
public class EkomCatalogListViewController : ControllerBase
{
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 24;
    private const int MaxPageSize = 100;

    private readonly ControllerRequestHelper _reqHelper;

    public EkomCatalogListViewController(ControllerRequestHelper reqHelper)
    {
        _reqHelper = reqHelper;
    }

    [HttpGet]
    [UmbracoUserAuthorize]
    public async Task<IActionResult> Get(
        [FromQuery] Guid categoryKey,
        [FromQuery] bool recursive = false,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] string? storeAlias = null,
        CancellationToken ct = default)
    {
        if (categoryKey == Guid.Empty)
        {
            return BadRequest(new { error = "Category key is required." });
        }

        page = Math.Max(DefaultPage, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var category = await API.Catalog.Instance.GetCategoryAsync(categoryKey, storeAlias, ct: ct).ConfigureAwait(false);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category, storeAlias);

        var query = new ProductQuery
        {
            Page = page,
            PageSize = pageSize,
            SearchQuery = search?.Trim() ?? string.Empty,
            StoreAlias = storeAlias ?? string.Empty,
        };

        var products = recursive
            ? await category.ProductsRecursiveAsync(query, ct).ConfigureAwait(false)
            : await category.ProductsAsync(query, ct).ConfigureAwait(false);

        var countProducts = await API.Catalog.Instance.GetAllProductsAsync(
            new ProductQuery
            {
                StoreAlias = storeAlias ?? string.Empty,
                RaiseEvents = false,
            },
            ct: ct).ConfigureAwait(false);

        var allProducts = countProducts.Products.ToList();

        var response = new CatalogListViewResponse(
            Category: ToCategoryItem(category, allProducts),
            Breadcrumbs: ToBreadcrumbItems(category, allProducts),
            Categories: category.SubCategories.Select(x => ToCategoryItem(x, allProducts)).ToList(),
            Products: products.Products.Select(ToProductItem).ToList(),
            Recursive: recursive,
            Page: products.Page ?? page,
            PageSize: products.PageSize ?? pageSize,
            PageCount: products.PageCount ?? 1,
            ProductCount: products.ProductCount,
            TotalProductCount: products.TotalProductCount,
            Search: query.SearchQuery);

        return Ok(response);
    }

    private static CatalogListViewCategoryItem ToCategoryItem(ICategory category, IReadOnlyCollection<IProduct> allProducts)
    {
        var productCount = CountProductsRecursive(category, allProducts);

        return new CatalogListViewCategoryItem(
            Id: category.Id,
            Key: category.Key,
            ParentId: category.ParentId,
            Title: category.Title,
            HasProducts: productCount > 0,
            ProductCount: productCount);
    }

    private static IReadOnlyList<CatalogListViewCategoryItem> ToBreadcrumbItems(ICategory category, IReadOnlyCollection<IProduct> allProducts)
    {
        var breadcrumbs = category.Ancestors
            .Where(x => x.Id != category.Id)
            .OrderBy(x => x.Level)
            .Select(x => ToCategoryItem(x, allProducts))
            .ToList();

        breadcrumbs.Add(ToCategoryItem(category, allProducts));

        return breadcrumbs;
    }

    private static int CountProductsRecursive(ICategory category, IEnumerable<IProduct> allProducts)
    {
        var categoryIds = category.SubCategoriesRecursive
            .Select(x => x.Id)
            .Append(category.Id)
            .ToHashSet();

        return allProducts.Count(x => categoryIds.Contains(x.ParentId));
    }

    private static CatalogListViewProductItem ToProductItem(IProduct product)
    {
        var variants = product.AllVariants.ToList();
        var image = product.Images.FirstOrDefault();

        return new CatalogListViewProductItem(
            Id: product.Id,
            Key: product.Key,
            ParentId: product.ParentId,
            Title: product.Title,
            Sku: product.SKU,
            ImageUrl: image?.Url ?? string.Empty,
            Price: product.Price.WithVat.CurrencyString,
            Stock: product.Stock,
            Available: product.Available,
            HasVariants: variants.Count > 0,
            VariantCount: variants.Count);
    }

    private sealed record CatalogListViewResponse(
        CatalogListViewCategoryItem Category,
        IReadOnlyList<CatalogListViewCategoryItem> Breadcrumbs,
        IReadOnlyList<CatalogListViewCategoryItem> Categories,
        IReadOnlyList<CatalogListViewProductItem> Products,
        bool Recursive,
        int Page,
        int PageSize,
        int PageCount,
        int ProductCount,
        int TotalProductCount,
        string Search);

    private sealed record CatalogListViewCategoryItem(
        int Id,
        Guid Key,
        int ParentId,
        string Title,
        bool HasProducts,
        int ProductCount);

    private sealed record CatalogListViewProductItem(
        int Id,
        Guid Key,
        int ParentId,
        string Title,
        string Sku,
        string ImageUrl,
        string Price,
        decimal Stock,
        bool Available,
        bool HasVariants,
        int VariantCount);
}
