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
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Guid}")]
    public IActionResult GetProduct(Guid Id)
    {
        try
        {
            var product = API.Catalog.Instance.GetProduct(Id);

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
    /// <returns></returns>
    [HttpGet]
    [Route("product/{id:Int}")]
    public IActionResult GetProduct(int Id)
    {
        try
        {
            var product = API.Catalog.Instance.GetProduct(Id);

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
    /// <returns></returns>
    [HttpGet]
    [Route("product/sku/{sku}")]
    public IActionResult GetProduct(string sku)
    {
        try
        {
            var product = API.Catalog.Instance.GetProduct(sku);

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
    /// <returns></returns>
    [HttpGet]
    [Route("product/route")]
    public IActionResult GetProductByRoute([FromQuery] string route)
    {
        try
        {
            var product = API.Catalog.Instance.GetProductByRoute(route);

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
    /// Get Recursive Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product Query</param>
    /// <returns></returns>
    [HttpPost, HttpGet]
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
    [HttpPost, HttpGet]
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
    /// Get Child Products Of A Category
    /// </summary>
    /// <param name="categoryId">Id of category</param>
    /// <param name="query">Product query</param>
    /// <returns></returns>
    [HttpPost,HttpGet]
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
    [HttpPost,HttpGet]
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
    [HttpPost, HttpGet]
    [Route("productsbyids")]
    public IActionResult GetProductsByIds([FromBody] ProductQuery? query = null)
    {
        try
        {
            if (query == null)
            {
                return BadRequest();
            }

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
    [HttpPost, HttpGet]
    [Route("productsbykeys")]
    public IActionResult GetProductsByKeys([FromBody] ProductQuery? query = null)
    {
        try
        {
            if (query == null)
            {
                return BadRequest();
            }

            var productsResponse = API.Catalog.Instance.GetProductsByKeys(query);

            return Ok(productsResponse);
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
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Int}")]
    public IActionResult GetCategory(int Id)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(Id);

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
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("category/{id:Guid}")]
    public IActionResult GetCategory(Guid Id)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(Id.ToString());

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
    /// <returns></returns>
    [HttpGet,HttpPost]
    [Route("category/route")]
    public IActionResult GetCategoryByRoute([FromQuery] string route)
    {
        try
        {
            if (string.IsNullOrEmpty(route))
            {
                return BadRequest();
            }

            var category = API.Catalog.Instance.GetCategoryByRoute(route);

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
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoriesbykeys")]
    public IActionResult GetCategoriesByKeys([FromBody]Guid[] keys)
    {
        try
        {
            if (keys == null || keys.Length <= 0)
            {
                return BadRequest();
            }

            var categories = API.Catalog.Instance.GetCategoriesByKeys(keys);

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
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoriesbyids")]
    public IActionResult GetCategoriesByIds([FromBody] int[] ids)
    {
        try
        {
            var categories = API.Catalog.Instance.GetCategoriesByIds(ids);

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
    public IActionResult GetRootCategories()
    {
        try
        {
            var categories = API.Catalog.Instance.GetRootCategories();

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
    public IActionResult GetAllCategories()
    {
        try
        {
            var categories = API.Catalog.Instance.GetAllCategories();

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
    public IActionResult GetSubCategories(int id)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(id);

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
    public IActionResult GetSubCategories(Guid key)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(key);

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
    public IActionResult GetSubCategoriesRecurisve(int id)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(id);

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
    public IActionResult GetSubCategoriesRecurisve(Guid key)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(key);

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
    public IActionResult GetCategoryFilters(int id)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(id);

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
    /// Get Category Filters
    /// </summary>
    /// <returns></returns>
    [HttpPost, HttpGet]
    [Route("categoryfilters/{key:Guid}")]
    public IActionResult GetCategoryFilters(Guid key)
    {
        try
        {
            var category = API.Catalog.Instance.GetCategory(key);

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
    public IActionResult GetRelatedProducts([FromRoute] Guid id, [FromRoute] int count = 4)
    {
        try
        {
            var products = API.Catalog.Instance.GetRelatedProducts(id, count);

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
    public IActionResult GetRelatedProducts([FromQuery] IEnumerable<Guid> ids, [FromQuery] int count = 4)
    {
        try
        {
            List<IProduct> relatedProducts = new List<IProduct>();

            foreach (Guid id in ids)
            {
                var products = API.Catalog.Instance.GetRelatedProducts(id, count);
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
    public IActionResult GetRelatedProductsBySku([FromRoute] string sku, [FromRoute] int count = 4)
    {
        try
        {
            var products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count);

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
    public IActionResult GetRelatedProductsBySku([FromQuery] IEnumerable<string> skus, [FromQuery] int count = 4)
    {
        try
        {
            List<IProduct> relatedProducts = new List<IProduct>();

            foreach (var sku in skus)
            {
                var products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count);
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
    [HttpPost, HttpGet]
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
