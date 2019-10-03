using System;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
    public class EkomController : UmbracoAuthorizedController
    {
        public ActionResult Manager()
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }

        public ActionResult Orders()
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }

        public ActionResult Order(Guid orderId)
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }

    }
}
