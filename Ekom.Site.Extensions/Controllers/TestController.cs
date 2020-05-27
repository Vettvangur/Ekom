using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Ekom.Interfaces;
using Ekom.Utilities;
using Umbraco.Web.Mvc;

namespace Ekom.Site.Extensions.Controllers
{
    public class TestController : SurfaceController
    {
        private readonly IConfigHelper _configHelper;

        public TestController(IConfigHelper configHelper)
        {
            _configHelper = configHelper;
        }

        public JsonResult Urls()
        {

            var urlMode = _configHelper.GetUrlModeCached();

            var urls = new List<string>()
            {
                "localhost:54961",
                "localhost:54961/en",
                "localhost",
                "www.ekom.is",
                "https://www.ekom.is",
                "www.ekom.is/en",
                "www.ekom.de",
                "www.ekom.de/en",
            };

            var list = new List<string>();


            foreach (var u in urls)
            {
                string domainPath = Ekom.Utilities.UrlHelper.GetDomainPrefix(u);

                list.Add(domainPath);
            }

            return Json(new
            {
                mode = urlMode.ToString(),
                list,
                distinct = list.Distinct()
            },JsonRequestBehavior.AllowGet);
        }
    }
}
