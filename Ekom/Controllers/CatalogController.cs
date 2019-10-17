using Ekom.Interfaces;
using Ekom.Utilities;
using System;
using System.Web.Http;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;

namespace Ekom.Controllers
{
    /// <summary>
    /// Product catalog
    /// </summary>
    [PluginController("Ekom")]
    public class CatalogController : UmbracoApiController
    {
        /// <summary>
        /// ctor
        /// </summary>
        public CatalogController()
        {
        }

        /// <summary>
        /// Get Product By Id
        /// </summary>
        /// <param name="Id">Guid Key of product</param>
        /// <returns></returns>
        public IProduct GetProduct(Guid Id)
        {
            try
            {
                return API.Catalog.Instance.GetProduct(Id);
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle<HttpResponseException>(ex);
                throw;
            }
        }
    }
}
