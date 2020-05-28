using System;
using System.Web.Http;
using System.Web.Mvc;
using Umbraco.Web.Mvc;

namespace Ekom.Manager.Controllers
{
    public class EkomController : UmbracoAuthorizedController
    {
        public ActionResult Index()
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }

        public ActionResult Orders()
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }

        public ActionResult Order()
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }
        public ActionResult Customers([FromUri] string email = "")
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }
        public ActionResult Customer()
        {
            return View("~/Views/EkomManager/Index.cshtml");
        }
    }
}
