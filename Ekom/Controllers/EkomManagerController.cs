using System.Web.Mvc;
using System.Web.Security;
using Umbraco.Web.Mvc;

namespace Ekom.Controllers
{
    public class EkomManagerController : UmbracoAuthorizedController
    {
        public ActionResult Index()
        {
            if (Members.GetCurrentMemberId() == -1)
            {
                // This allows admins to bypass the umbraco content access controls
                FormsAuthentication.SetAuthCookie("adminproxy", true);
            }

            return View();
        }
    }
}
