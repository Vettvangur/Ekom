using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
    public class EkomCatalogController : SurfaceController
    {
        ILog _log;

        /// <summary>
        /// ctor
        /// </summary>
        public EkomCatalogController()
        {
            var logFac = Configuration.container.GetInstance<ILogFactory>();
            _log = logFac.GetLogger(typeof(EkomOrderController));
        }

        /// <summary>
        /// Get Product By Id
        /// </summary>
        /// <param name="Id">Guid Key of product</param>
        /// <returns></returns>
        public object GetProduct(Guid Id)
        {
            return API.Catalog.Current.GetProduct(Id);
        }
    }
}
