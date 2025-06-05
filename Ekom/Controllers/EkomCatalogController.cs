using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers;

/// <summary>
/// Product catalog
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Reliability",
    "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "Async controller actions don't need ConfigureAwait")]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style",
    "VSTHRD200:Use \"Async\" suffix for async methods",
    Justification = "Async controller action")]
[Route("ekom/catalog")]
[ServiceFilter(typeof(ApiExceptionFilter))]
public class EkomCatalogController : ControllerBase
{
    private readonly ControllerRequestHelper _reqHelper;

    /// <summary>
    /// ctor
    /// </summary>
    public EkomCatalogController(ControllerRequestHelper reqHelper)
    {
        _reqHelper = reqHelper;
    }

    /// <summary>
    /// Get Product By Id
    /// </summary>
    /// <param name="Id">Guid Key of product</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Guid}")]
    public IActionResult GetProduct(Guid Id)
    {
        IProduct? product = API.Catalog.Instance.GetProduct(Id);

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
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Int}")]
    public IActionResult GetProduct(int Id)
    {
        IProduct? product = API.Catalog.Instance.GetProduct(Id);

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
    /// <returns></returns>
    [HttpGet]
    [Route("product/sku/{sku}")]
    public IActionResult GetProduct(string sku)
    {
        IProduct? product = API.Catalog.Instance.GetProduct(sku);

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
    /// <returns></returns>
    [HttpGet]
    [Route("product/route")]
    public IActionResult GetProductByRoute([FromQuery] string route)
    {
        IProduct? product = API.Catalog.Instance.GetProductByRoute(route);

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
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsrecursive/route")]
    public IActionResult GetProductsRecursiveByRoute([FromQuery] string route, [FromBody] ProductQuery? query = null)
    {
        _reqHelper.SetEkmRequest(storeAlias: query?.StoreAlias);

        ProductResponse? products = API.Catalog.Instance.GetProductsRescursiveByRoute(route, query);

        return Ok(products);
    }

    /// <summary>
    /// Get Recursive Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsrecursive/{categoryId:Int}")]
    public IActionResult GetProductsRecursive(int categoryId, [FromBody] ProductQuery? query = null)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(categoryId, query?.StoreAlias);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(category.ProductsRecursive(query));
    }

    /// <summary>
    /// Get Recursive Products Of A Category
    /// </summary>
    /// <param name="categoryKey">Key of category</param
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsrecursive/{categoryKey:Guid}")]
    public IActionResult GetProductsRecursive(Guid categoryKey, [FromBody] ProductQuery? query = null)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(categoryKey, query?.StoreAlias);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(category.ProductsRecursive(query));
    }

    /// <summary>
    /// Get Child Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product query</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("products/{categoryId:Int}")]
    public IActionResult GetProducts(int categoryId, [FromBody] ProductQuery? query = null)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(categoryId, query?.StoreAlias);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(category.Products(query));
    }

    /// <summary>
    /// Get Child Products Of A Category
    /// </summary>
    /// <param name="categoryKey">Key of category</param>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("products/{categoryKey:Guid}")]
    public IActionResult GetProducts(Guid categoryKey, [FromBody] ProductQuery? query = null)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(categoryKey, query?.StoreAlias);

        if (category == null)
        {
            return NotFound();
        }

        _reqHelper.SetEkmRequest(category);

        return Ok(category.Products(query));
    }

    /// <summary>
    /// Get Products By Ids
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsbyids")]
    public IActionResult GetProductsByIds([FromBody] ProductQuery? query = null)
    {
        if (query == null)
        {
            return BadRequest();
        }

        _reqHelper.SetEkmRequest(storeAlias: query?.StoreAlias);

        ProductResponse productsResponse = API.Catalog.Instance.GetProductsByIds(query);

        return Ok(productsResponse);
    }

    /// <summary>
    /// Get Products By Keys
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsbykeys")]
    public IActionResult GetProductsByKeys([FromBody] ProductQuery? query = null)
    {
        if (query == null)
        {
            return BadRequest();
        }

        _reqHelper.SetEkmRequest(storeAlias: query?.StoreAlias);

        ProductResponse productsResponse = API.Catalog.Instance.GetProductsByKeys(query);

        return Ok(productsResponse);
    }

    /// <summary>
    /// Get Category By Id
    /// </summary>
    /// <param name="Id">Int Id of category</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Int}")]
    public IActionResult GetCategory(int Id)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(Id);

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
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Guid}")]
    public IActionResult GetCategory(Guid Id)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(Id.ToString());

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
    /// <returns></returns>
    [HttpGet, HttpPost]
    [Route("category/route")]
    public IActionResult GetCategoryByRoute([FromQuery] string route)
    {
        if (string.IsNullOrEmpty(route))
        {
            return BadRequest();
        }

        ICategory? category = API.Catalog.Instance.GetCategoryByRoute(route);

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
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoriesbykeys")]
    public IActionResult GetCategoriesByKeys([FromBody] Guid[] keys)
    {
        if (keys == null || keys.Length <= 0)
        {
            return BadRequest();
        }

        IEnumerable<ICategory> categories = API.Catalog.Instance.GetCategoriesByKeys(keys);

        return Ok(categories);
    }

    /// <summary>
    /// Get Categories By Ids
    /// </summary>
    /// <param name="ids">Int[] ids of categories</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoriesbyids")]
    public IActionResult GetCategoriesByIds([FromBody] int[] ids)
    {
        IEnumerable<ICategory> categories = API.Catalog.Instance.GetCategoriesByIds(ids);

        return Ok(categories);
    }

    /// <summary>
    /// Get Root Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("rootcategories")]
    public IActionResult GetRootCategories()
    {
        IEnumerable<ICategory> categories = API.Catalog.Instance.GetRootCategories();

        return Ok(categories);
    }

    /// <summary>
    /// Get All Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("allcategories")]
    public IActionResult GetAllCategories()
    {
        IEnumerable<ICategory> categories = API.Catalog.Instance.GetAllCategories();

        return Ok(categories);
    }

    /// <summary>
    /// Get Sub Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategories/{id:Int}")]
    public IActionResult GetSubCategories(int id)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(id);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategories);
    }

    /// <summary>
    /// Get Sub Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategories/{key:Guid}")]
    public IActionResult GetSubCategories(Guid key)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(key);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategories);
    }


    /// <summary>
    /// Get Sub Categories Recursive
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategoriesrecursive/{id:Int}")]
    public IActionResult GetSubCategoriesRecurisve(int id)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(id);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategoriesRecursive);
    }

    /// <summary>
    /// Get Sub Categories Recursive
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategoriesrecursive/{key:Guid}")]
    public IActionResult GetSubCategoriesRecurisve(Guid key)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(key);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.SubCategoriesRecursive);
    }


    /// <summary>
    /// Get Category Filters
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoryfilters/{id:Int}")]
    public IActionResult GetCategoryFilters(int id)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(id);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.Filters());
    }

    /// <summary>
    /// Get Category Filters
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoryfilters/{key:Guid}")]
    public IActionResult GetCategoryFilters(Guid key)
    {
        ICategory? category = API.Catalog.Instance.GetCategory(key);

        if (category == null)
        {
            return NotFound();
        }

        return Ok(category.Filters());
    }


    /// <summary>
    /// Get Related Products
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproducts/{id:Guid}/{count:Int}")]
    public IActionResult GetRelatedProducts([FromRoute] Guid id, [FromRoute] int count = 4)
    {
        IEnumerable<IProduct> products = API.Catalog.Instance.GetRelatedProducts(id, count);

        return Ok(products);
    }

    /// <summary>
    /// Get Related Products
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproducts")]
    public IActionResult GetRelatedProducts([FromQuery] IEnumerable<Guid> ids, [FromQuery] int count = 4)
    {
        var relatedProducts = new List<IProduct>();

        foreach (Guid id in ids)
        {
            IEnumerable<IProduct> products = API.Catalog.Instance.GetRelatedProducts(id, count);
            relatedProducts.AddRange(products);
        }

        return Ok(relatedProducts.Take(count));
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    /// <returns></returns>a
    [HttpPost, HttpGet]
    [Route("relatedproductsbysku/{sku}/{count:Int}")]
    public IActionResult GetRelatedProductsBySku([FromRoute] string sku, [FromRoute] int count = 4)
    {
        IEnumerable<IProduct> products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count);

        return Ok(products);
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproductsbyskus")]
    public IActionResult GetRelatedProductsBySku([FromQuery] IEnumerable<string> skus, [FromQuery] int count = 4)
    {
        var relatedProducts = new List<IProduct>();

        foreach (string sku in skus)
        {
            IEnumerable<IProduct> products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count);
            relatedProducts.AddRange(products);
        }

        return Ok(relatedProducts.Take(count));
    }

    /// <summary>
    /// Product Search
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("productsearch")]
    public IActionResult ProductSearch([FromBody] SearchRequest req)
    {
        ProductResponse products = API.Catalog.Instance.ProductSearch(req);

        return Ok(products);
    }

}
