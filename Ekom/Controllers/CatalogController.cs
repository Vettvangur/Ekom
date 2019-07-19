using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using System;
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
            return API.Catalog.Instance.GetProduct(Id);
        }
    }
}
