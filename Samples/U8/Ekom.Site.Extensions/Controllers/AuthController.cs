namespace Ekom.Site.Extensions.Controllers
{
    //public class AuthController : SurfaceController
    //{
    //    public ActionResult Login(string username, string password)
    //    {
    //        try
    //        {
    //            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
    //            {
    //                if (Membership.ValidateUser(username, password))
    //                {
    //                    FormsAuthentication.SetAuthCookie(username, true);

    //                    return RedirectToUmbracoPage(UmbracoContext.PublishedContentRequest.PublishedContent.Children.First().Id);
    //                }
    //            }

    //            TempData["error"] = "Username or password incorrect.";

    //        }
    //        catch (Exception ex)
    //        {
    //            TempData["error"] = "Server error.";

    //            Log.Error("Login server error", ex);
    //        }

    //        return RedirectToCurrentUmbracoPage();
    //    }

    //    public ActionResult Logout()
    //    {
    //        FormsAuthentication.SignOut();

    //        return RedirectToUmbracoPage(UmbracoContext.PublishedContentRequest.PublishedContent.Site().Id);
    //    }

    //    private static readonly ILog Log =
    //        LogManager.GetLogger(
    //            MethodBase.GetCurrentMethod().DeclaringType
    //        );
    //}
}
