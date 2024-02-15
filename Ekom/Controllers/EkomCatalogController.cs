using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers
{
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
        /// <summary>
        /// ctor
        /// </summary>
        public EkomCatalogController()
        {
        }

        /// <summary>
        /// Get Product By Id
        /// </summary>
        /// <param name="Id">Guid Key of product</param>
        /// <returns></returns>
        [HttpGet]
        [Route("product/{id:Guid}")]
        public IProduct GetProduct(Guid Id)
        {
            try
            {
                return API.Catalog.Instance.GetProduct(Id);
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
        public IProduct GetProduct(int Id)
        {
            try
            {
                return API.Catalog.Instance.GetProduct(Id);
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
        public IProduct GetProduct(string sku)
        {
            try
            {
                return API.Catalog.Instance.GetProduct(sku);
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
        /// <returns></returns>
        [HttpPost]
        [Route("products/{categoryId:Int}")]
        public ProductResponse GetProducts(int categoryId, [FromBody] ProductQuery query = null)
        {
            try
            {
                var category = API.Catalog.Instance.GetCategory(categoryId);

                return category.Products(query);
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
        public ProductResponse GetProductsByIds([FromBody] ProductQuery query = null)
        {
            try
            {
                return API.Catalog.Instance.GetProductsByIds(query);
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
        public ProductResponse GetProductsByKeys([FromBody] ProductQuery query = null)
        {
            try
            {
                return API.Catalog.Instance.GetProductsByKeys(query);
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
        /// <returns></returns>
        [HttpPost]
        [Route("productsrecursive/{categoryId:Int}")]
        public ProductResponse GetProductsRecursive(int categoryId, [FromBody] ProductQuery query = null)
        {
            try
            {
                var category = API.Catalog.Instance.GetCategory(categoryId);

                if (category == null)
                {
                    throw new ArgumentNullException(nameof(category));
                }

                return category.ProductsRecursive(query);
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
        public ICategory GetCategory(int Id)
        {
            try
            {
                return API.Catalog.Instance.GetCategory(Id);
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
        public ICategory GetCategory(Guid Id)
        {
            try
            {
                return API.Catalog.Instance.GetCategory(Id.ToString());
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
        public IEnumerable<ICategory> GetRootCategories()
        {
            try
            {
                return API.Catalog.Instance.GetRootCategories();
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
        public IEnumerable<ICategory> GetAllCategories()
        {
            try
            {
                return API.Catalog.Instance.GetAllCategories();
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
        public IEnumerable<ICategory> GetSubCategories(int id)
        {
            try
            {
                var category = API.Catalog.Instance.GetCategory(id);

                return category.SubCategories;
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
        public IEnumerable<ICategory> GetSubCategoriesRecurisve(int id)
        {
            try
            {

                var category = API.Catalog.Instance.GetCategory(id);

                return category.SubCategoriesRecursive;
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
        public IEnumerable<MetafieldGrouped> GetCategoryFilters(int id)
        {
            try
            {
                var category = API.Catalog.Instance.GetCategory(id);

                return category.Filters();
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
        public IEnumerable<IProduct> GetRelatedProducts(Guid id, int count = 4)
        {
            try
            {
                var products = API.Catalog.Instance.GetRelatedProducts(id, count);

                return products;
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
        public IEnumerable<IProduct> GetRelatedProducts(IEnumerable<Guid> ids, int count = 4)
        {
            try
            {
                List<IProduct> relatedProducts = new List<IProduct>();

                foreach (Guid id in ids)
                {
                    var products = API.Catalog.Instance.GetRelatedProducts(id, count);
                    relatedProducts.AddRange(products);
                }

                return relatedProducts.Take(count);
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
        public IEnumerable<IProduct> GetRelatedProductsBySku(string sku, int count = 4)
        {
            try
            {
                var products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count);

                return products;
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
        public IEnumerable<IProduct> GetRelatedProductsBySku(IEnumerable<string> skus, int count = 4)
        {
            try
            {
                List<IProduct> relatedProducts = new List<IProduct>();

                foreach (var sku in skus)
                {
                    var products = API.Catalog.Instance.GetRelatedProductsBySku(sku, count);
                    relatedProducts.AddRange(products);
                }

                return relatedProducts.Take(count);
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
        public ProductResponse ProductSearch([FromBody] SearchRequest req)
        {
            try
            {                
                var products = API.Catalog.Instance.ProductSearch(req);

                return products;
            }
            catch (Exception ex) when (!(ex is HttpResponseException))
            {
                throw ExceptionHandler.Handle<HttpResponseException>(ex);
            }
        }

    }
}
