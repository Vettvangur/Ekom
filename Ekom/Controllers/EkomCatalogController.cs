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
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Guid}")]
    public IActionResult GetProduct(Guid Id, string? storeAlias = null)
    {
        try
        {
            var product = API.Catalog.Instance.GetProduct(Id, storeAlias);

            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Product By Id
    /// </summary>
    /// <param name="Id">Int Id of product</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Int}")]
    public IActionResult GetProduct(int Id, string? storeAlias = null)
    {
        try
        {
            var product = API.Catalog.Instance.GetProduct(Id, storeAlias);

            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Product By Sku
    /// </summary>
    /// <param name="sku">Sku of product</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/sku/{sku}")]
    public IActionResult GetProduct(string sku, string? storeAlias = null)
    {
        try
        {
            var product = API.Catalog.Instance.GetProduct(sku, storeAlias);

            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        } 
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Product By Route
    /// </summary>
    /// <param name="route">Route</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpGet]
    [Route("product/route")]
    public IActionResult GetProductByRoute([FromQuery] string route,[FromQuery] string? storeAlias = null)
    {
        try
        {
            var product = API.Catalog.Instance.GetProductByRoute(route, storeAlias);

            if (product == null)
            {
                return NotFound();
            }

            return Ok(product);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Product By Route
    /// </summary>
    /// <param name="route">Route</param>
    /// <param name="query">Product query</param>
    /// <returns></returns>
    [HttpGet]
    [Route("productsrecursive/route")]
    public IActionResult GetProductsRecursiveByRoute([FromQuery] string route, [FromBody] ProductQuery? query = null)
    {
        try
        {
            var products = API.Catalog.Instance.GetProductsRescursiveByRoute(route, query);

            return Ok(products);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Child Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product query</param>
    /// <returns></returns>
    [HttpPost]
    [Route("products/{categoryId:Int}")]
    public IActionResult GetProducts(int categoryId, [FromBody] ProductQuery? query = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(categoryId, query?.StoreAlias);

            if (category == null)
            {
                return NotFound();
            }

            _reqHelper.SetEkmRequest(category);

            return Ok(category.Products(query));
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Child Products Of A Category
    /// </summary>
    /// <param name="categoryKey">Key of category</param>
    /// <param name="query"></param>
    /// <returns></returns>
    [HttpPost]
    [Route("products/{categoryKey:Guid}")]
    public IActionResult GetProducts(Guid categoryKey, [FromBody] ProductQuery? query = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(categoryKey, query?.StoreAlias);

            if (category == null)
            {
                return NotFound();
            }

            _reqHelper.SetEkmRequest(category);

            return Ok(category.Products(query));
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Products By Ids
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost]
    [Route("productsbyids")]
    public IActionResult GetProductsByIds([FromBody] ProductQuery? query = null)
    {
        try
        {
            var productsResponse = API.Catalog.Instance.GetProductsByIds(query);

            return Ok(productsResponse);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Products By Keys
    /// </summary>
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost]
    [Route("productsbykeys")]
    public IActionResult GetProductsByKeys([FromBody] ProductQuery? query = null)
    {
        try
        {
            var productsResponse = API.Catalog.Instance.GetProductsByKeys(query);

            return Ok(productsResponse);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Recursive Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost]
    [Route("productsrecursive/{categoryId:Int}")]
    public IActionResult GetProductsRecursive(int categoryId, [FromBody] ProductQuery? query = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(categoryId, query?.StoreAlias);

            if (category == null)
            {
                return NotFound();
            }

            _reqHelper.SetEkmRequest(category);

            return Ok(category.ProductsRecursive(query));
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Recursive Products Of A Category
    /// </summary>
    /// <param name="categoryKey">Key of category</param
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost]
    [Route("productsrecursive/{categoryKey:Guid}")]
    public IActionResult GetProductsRecursive(Guid categoryKey, [FromBody] ProductQuery? query = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(categoryKey, query?.StoreAlias);

            if (category == null)
            {
                return NotFound();
            }

            _reqHelper.SetEkmRequest(category);

            return Ok(category.ProductsRecursive(query));
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Category By Id
    /// </summary>
    /// <param name="Id">Int Id of category</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Int}")]
    public IActionResult GetCategory(int Id, string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(Id, storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category);

        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Category By Id
    /// </summary>
    /// <param name="Id">Int Id of category</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Guid}")]
    public IActionResult GetCategory(Guid Id, string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(Id.ToString(), storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Category By Route
    /// </summary>
    /// <param name="route">Route</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpGet]
    [Route("category/route")]
    public IActionResult GetCategoryByRoute([FromQuery] string route, [FromQuery] string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategoryByRoute(route, storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Categories By Keys
    /// </summary>
    /// <param name="keys">Guid[] keys of categories</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpPost]
    [Route("categoriesbykeys")]
    public IActionResult GetCategoriesByKeys([FromBody]Guid[] keys, string? storeAlias = null)
    {
        try
        {
            var categories = API.Catalog.Instance.GetCategoriesByKeys(keys, storeAlias);

            return Ok(categories);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Categories By Ids
    /// </summary>
    /// <param name="ids">Int[] ids of categories</param>
    /// <param name="storeAlias">Store Alias</param>
    /// <returns></returns>
    [HttpPost]
    [Route("categoriesbyids")]
    public IActionResult GetCategoriesByIds([FromBody] int[] ids, string? storeAlias = null)
    {
        try
        {
            var categories = API.Catalog.Instance.GetCategoriesByIds(ids, storeAlias);

            return Ok(categories);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Root Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("rootcategories")]
    public IActionResult GetRootCategories(string? storeAlias = null)
    {
        try
        {
            var categories = API.Catalog.Instance.GetRootCategories(storeAlias);

            return Ok(categories);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get All Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("allcategories")]
    public IActionResult GetAllCategories(string? storeAlias = null)
    {
        try
        {
            var categories = API.Catalog.Instance.GetAllCategories(storeAlias);

            return Ok(categories);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Sub Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategories/{id:Int}")]
    public IActionResult GetSubCategories(int id, string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(id, storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category.SubCategories);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Sub Categories
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategories/{key:Guid}")]
    public IActionResult GetSubCategories(Guid key, string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(key, storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category.SubCategories);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }


    /// <summary>
    /// Get Sub Categories Recursive
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategoriesrecursive/{id:Int}")]
    public IActionResult GetSubCategoriesRecurisve(int id, string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(id, storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category.SubCategoriesRecursive);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Sub Categories Recursive
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("subcategoriesrecursive/{key:Guid}")]
    public IActionResult GetSubCategoriesRecurisve(Guid key, string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(key, storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category.SubCategoriesRecursive);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }


    /// <summary>
    /// Get Category Filters
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoryfilters/{id:Int}")]
    public IActionResult GetCategoryFilters(int id, string? storeAlias = null)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(id, storeAlias);

            if (category == null)
            {
                return NotFound();
            }

            return Ok(category.Filters());
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }


    /// <summary>
    /// Get Related Products
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproducts/{id:Guid}/{count:Int}")]
    public IActionResult GetRelatedProducts(Guid id, int count = 4, string? storeAlias = null)
    {
        try
        {
            var products = API.Catalog.Instance.GetRelatedProducts(id, count, storeAlias);

            return Ok(products);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Related Products
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproducts")]
    public IActionResult GetRelatedProducts(IEnumerable<Guid> ids, int count = 4, string? storeAlias = null)
    {
        try
        {
            List<IProduct> relatedProducts = new List<IProduct>();

            foreach (Guid id in ids)
            {
                var products = API.Catalog.Instance.GetRelatedProducts(id, count, storeAlias);
                relatedProducts.AddRange(products);
            }

            return Ok(relatedProducts.Take(count));
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    /// <returns></returns>a
    [HttpPost, HttpGet]
    [Route("relatedproductsbysku/{sku}/{count:Int}")]
    public IActionResult GetRelatedProductsBySku(string sku, int count = 4, string? storeAlias = null)
    {
        try
        {
            var products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count, storeAlias);

            return Ok(products);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Get Related Products By Sku
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("relatedproductsbyskus")]
    public IActionResult GetRelatedProductsBySku(IEnumerable<string> skus, int count = 4, string? storeAlias = null)
    {
        try
        {
            List<IProduct> relatedProducts = new List<IProduct>();

            foreach (var sku in skus)
            {
                var products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count, storeAlias);
                relatedProducts.AddRange(products);
            }

            return Ok(relatedProducts.Take(count));
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

    /// <summary>
    /// Product Search
    /// </summary>
    /// <returns></returns>
    [HttpPost]
    [Route("productsearch")]
    public IActionResult ProductSearch([FromBody] SearchRequest req)
    {
        try
        {                
            var products = API.Catalog.Instance.ProductSearch(req);

            return Ok(products);
        }
        catch (Exception ex) when (!(ex is HttpResponseException))
        {
            throw ExceptionHandler.Handle<HttpResponseException>(ex);
        }
    }

}
