using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers;

/// <summary>
/// Product catalog
/// </summary>
[Route("ekom/catalog")]
[ServiceFilter(typeof(ApiExceptionFilter))]
public class EkomCatalogController : ControllerBase
{
    private readonly ControllerRequestHelper _reqHelper;
    private readonly IMetafieldService _metafieldService;
    /// <summary>
    /// ctor
    /// </summary>
    public EkomCatalogController(ControllerRequestHelper reqHelper, IMetafieldService metafieldService)
    {
        _reqHelper = reqHelper;
        _metafieldService = metafieldService;
    }

    /// <summary>
    /// Get Product By Id
    /// </summary>
    /// <param name="Id">Guid Key of product</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Guid}")]
    public async Task<IActionResult> GetProductAsync(Guid Id, CancellationToken ct = default)
    {
        IProduct? product = await API.Catalog.Instance.GetProductAsync(Id, ct: ct);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    /// <summary>
    /// Get Product By Id
    /// </summary>
    /// <param name="Id">Int Id of product</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Int}")]
    public async Task<IActionResult> GetProductAsync(int Id, CancellationToken ct = default)
    {
        IProduct? product = await API.Catalog.Instance.GetProductAsync(Id, ct: ct);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    /// <summary>
    /// Get Product By Sku
    /// </summary>
    /// <param name="sku">Sku of product</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/sku/{sku}")]
    public async Task<IActionResult> GetProductAsync(string sku, CancellationToken ct = default)
    {
        IProduct? product = await API.Catalog.Instance.GetProductAsync(sku, ct: ct);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    /// <summary>
    /// Get Product By Route
    /// </summary>
    /// <param name="route">Route</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/route")]
    public async Task<IActionResult> GetProductByRouteAsync([FromQuery] string route, CancellationToken ct = default)
    {
        IProduct? product = await API.Catalog.Instance.GetProductByRouteAsync(route, ct: ct);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    /// <summary>
    /// Get Product By Route
    /// </summary>
    /// <param name="route">Route</param>
    /// <param name="query">Product query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsrecursive/route")]
    public async Task<IActionResult> GetProductsRecursiveByRouteAsync([FromQuery] string route, [FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        _reqHelper.SetEkmRequest(storeAlias: query?.StoreAlias);

        ProductResponse? products = await API.Catalog.Instance.GetProductsRescursiveByRouteAsync(route, query, ct: ct);
        return Ok(products);
    }

    /// <summary>
    /// Get Recursive Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product Query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsrecursive/{categoryId:Int}")]
    public async Task<IActionResult> GetProductsRecursiveAsync(int categoryId, [FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(categoryId, query?.StoreAlias, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(await category.ProductsRecursiveAsync(query, ct: ct));
    }

    /// <summary>
    /// Get Recursive Products Of A Category
    /// </summary>
    /// <param name="categoryKey">Key of category</param>
    /// <param name="query">Product Query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsrecursive/{categoryKey:Guid}")]
    public async Task<IActionResult> GetProductsRecursiveAsync(Guid categoryKey, [FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(categoryKey, query?.StoreAlias, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(await category.ProductsRecursiveAsync(query, ct: ct));
    }

    /// <summary>
    /// Get Child Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("products/{categoryId:Int}")]
    public async Task<IActionResult> GetProductsAsync(int categoryId, [FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(categoryId, query?.StoreAlias, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(await category.ProductsAsync(query, ct));
    }

    /// <summary>
    /// Get Child Products Of A Category
    /// </summary>
    /// <param name="categoryKey">Key of category</param>
    /// <param name="query"></param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("products/{categoryKey:Guid}")]
    public async Task<IActionResult> GetProductsAsync(Guid categoryKey, [FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(categoryKey, query?.StoreAlias, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(await category.ProductsAsync(query, ct: ct));
    }

    /// <summary>
    /// Get Products By Ids
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsbyids")]
    public async Task<IActionResult> GetProductsByIdsAsync([FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        if (query == null)
        {
            return BadRequest();
        }

        _reqHelper.SetEkmRequest(storeAlias: query?.StoreAlias);

        var productsResponse = await API.Catalog.Instance.GetProductsByIdsAsync(query, ct: ct);

        return Ok(productsResponse);
    }

    /// <summary>
    /// Get Products By Keys
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsbykeys")]
    public async Task<IActionResult> GetProductsByKeysAsync([FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        if (query == null)
        {
            return BadRequest();
        }

        _reqHelper.SetEkmRequest(storeAlias: query?.StoreAlias);

        ProductResponse productsResponse = await API.Catalog.Instance.GetProductsByKeysAsync(query, ct: ct);

        return Ok(productsResponse);
    }

    /// <summary>
    /// Get Products By Skus
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsbyskus")]
    public async Task<IActionResult> GetProductsBySkusAsync([FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        if (query == null)
        {
            return BadRequest();
        }

        _reqHelper.SetEkmRequest(storeAlias: query?.StoreAlias);

        ProductResponse productsResponse = await API.Catalog.Instance.GetProductsBySkusAsync(query, ct: ct);

        return Ok(productsResponse);
    }

    /// <summary>
    /// Get All Products
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("allproducts")]
    public async Task<IActionResult> GetAllProductsAsync([FromBody] ProductQuery? query = null, CancellationToken ct = default)
    {
        ProductResponse productsResponse = await API.Catalog.Instance.GetAllProductsAsync(query, ct: ct);

        return Ok(productsResponse);
    }

    /// <summary>
    /// Get Category By Id
    /// </summary>
    /// <param name="Id">Int Id of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Int}")]
    public async Task<IActionResult> GetCategoryAsync(int Id, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(Id, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category);
    }

    /// <summary>
    /// Get Category By Id
    /// </summary>
    /// <param name="Id">Int Id of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Guid}")]
    public async Task<IActionResult> GetCategoryAsync(Guid Id, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(Id.ToString(), ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category);
    }

    /// <summary>
    /// Get Category By Route
    /// </summary>
    /// <param name="route">Route</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpGet, HttpPost]
    [Route("category/route")]
    public async Task<IActionResult> GetCategoryByRouteAsync([FromQuery] string route, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(route))
        {
            return BadRequest();
        }

        ICategory? category = await API.Catalog.Instance.GetCategoryByRouteAsync(route, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category);
    }

    /// <summary>
    /// Get Categories By Keys
    /// </summary>
    /// <param name="keys">Guid[] keys of categories</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoriesbykeys")]
    public async Task<IActionResult> GetCategoriesByKeysAsync([FromBody] Guid[] keys, CancellationToken ct = default)
    {
        if (keys == null || keys.Length <= 0)
        {
            return BadRequest();
        }

        IEnumerable<ICategory> categories = await API.Catalog.Instance.GetCategoriesByKeysAsync(keys, ct: ct);

        return Ok(categories);
    }

    /// <summary>
    /// Get Categories By Ids
    /// </summary>
    /// <param name="ids">Int[] ids of categories</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoriesbyids")]
    public async Task<IActionResult> GetCategoriesByIdsAsync([FromBody] int[] ids, CancellationToken ct = default)
    {
        IEnumerable<ICategory> categories = await API.Catalog.Instance.GetCategoriesByIdsAsync(ids, ct: ct);

        return Ok(categories);
    }

    /// <summary>
    /// Get Root Categories
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("rootcategories")]
    public async Task<IActionResult> GetRootCategoriesAsync(CancellationToken ct = default)
    {
        IEnumerable<ICategory> categories = await API.Catalog.Instance.GetRootCategoriesAsync(ct: ct);

        return Ok(categories);
    }

    /// <summary>
    /// Get All Categories
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("allcategories")]
    public async Task<IActionResult> GetAllCategoriesAsync(CancellationToken ct = default)
    {
        IEnumerable<ICategory> categories = await API.Catalog.Instance.GetAllCategoriesAsync(ct: ct);

        return Ok(categories);
    }

    /// <summary>
    /// Get Sub Categories
    /// </summary>
    /// <param name="id">Int Id of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategories/{id:Int}")]
    public async Task<IActionResult> GetSubCategoriesAsync(int id, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(id, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategories);
    }

    /// <summary>
    /// Get Sub Categories
    /// </summary>
    /// <param name="key">Guid key of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategories/{key:Guid}")]
    public async Task<IActionResult> GetSubCategoriesAsync(Guid key, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(key, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategories);
    }


    /// <summary>
    /// Get Sub Categories Recursive
    /// </summary>
    /// <param name="id">Int Id of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategoriesrecursive/{id:Int}")]
    public async Task<IActionResult> GetSubCategoriesRecursiveAsync(int id, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(id, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategoriesRecursive);
    }

    /// <summary>
    /// Get Sub Categories Recursive
    /// </summary>
    /// <param name="key">Guid key of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategoriesrecursive/{key:Guid}")]
    public async Task<IActionResult> GetSubCategoriesRecursiveAsync(Guid key, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(key, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategoriesRecursive);
    }


    /// <summary>
    /// Get Category Filters
    /// </summary>
    /// <param name="id">Int Id of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoryfilters/{id:Int}")]
    public async Task<IActionResult> GetCategoryFiltersAsync(int id, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(id, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(await category.FiltersAsync(ct: ct));
    }

    /// <summary>
    /// Get Category Filters
    /// </summary>
    /// <param name="key">Guid key of category</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoryfilters/{key:Guid}")]
    public async Task<IActionResult> GetCategoryFiltersAsync(Guid key, CancellationToken ct = default)
    {
        ICategory? category = await API.Catalog.Instance.GetCategoryAsync(key, ct: ct);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(await category.FiltersAsync(ct: ct));
    }


    /// <summary>
    /// Get Related Products
    /// </summary>
    /// <param name="id">Guid Id of product</param>
    /// <param name="count">Number of related products to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproducts/{id:Guid}/{count:Int}")]
    public async Task<IActionResult> GetRelatedProductsAsync([FromRoute] Guid id, [FromRoute] int count = 4, CancellationToken ct = default)
    {
        IEnumerable<IProduct> products = await API.Catalog.Instance.GetRelatedProductsAsync(id, count, ct: ct);

        return Ok(products);
    }

    /// <summary>
    /// Get Related Products
    /// </summary>
    /// <param name="ids">Guid Ids of products</param>
    /// <param name="count">Number of related products to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproducts")]
    public async Task<IActionResult> GetRelatedProductsAsync([FromQuery] IEnumerable<Guid> ids, [FromQuery] int count = 4, CancellationToken ct = default)
    {
        var relatedProducts = new List<IProduct>();

        foreach (Guid id in ids)
        {
            IEnumerable<IProduct> products = await API.Catalog.Instance.GetRelatedProductsAsync(id, count, ct: ct);
            relatedProducts.AddRange(products);
        }

        return Ok(relatedProducts.Take(count));
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    /// <param name="sku">Sku of product</param>
    /// <param name="count">Number of related products to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproductsbysku/{sku}/{count:Int}")]
    public async Task<IActionResult> GetRelatedProductsBySkuAsync([FromRoute] string sku, [FromRoute] int count = 4, CancellationToken ct = default)
    {
        IEnumerable<IProduct> products = await API.Catalog.Instance.GetRelatedProductsBySkuAsync(sku, count, ct: ct);

        return Ok(products);
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    /// <param name="skus">Skus of products</param>
    /// <param name="count">Number of related products to return</param>
    /// <param name="ct">Cancellation token</param> 
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproductsbyskus")]
    public async Task<IActionResult> GetRelatedProductsBySkuAsync([FromQuery] IEnumerable<string> skus, [FromQuery] int count = 4, CancellationToken ct = default)
    {
        var relatedProducts = new List<IProduct>();

        foreach (string sku in skus)
        {
            IEnumerable<IProduct> products = await API.Catalog.Instance.GetRelatedProductsBySkuAsync(sku, count, ct: ct);
            relatedProducts.AddRange(products);
        }

        return Ok(relatedProducts.Take(count));
    }

    /// <summary>
    /// Product Search
    /// </summary>
    /// <param name="req">Search request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsearch")]
    public async Task<IActionResult> ProductSearchAsync([FromBody] SearchRequest req, CancellationToken ct = default)
    {
        ProductResponse products = await API.Catalog.Instance.ProductSearchAsync(req, ct: ct);

        return Ok(products);
    }

    /// <summary>
    /// Metafields
    /// </summary>
    /// <returns></returns>
    [HttpGet,HttpPost]
    [Route("metafields")]
    public IEnumerable<Metafield> GetMetafields()
        => _metafieldService.GetMetafields();

}
