using React.Web.Mvc;
using System;
using System.Configuration;
using System.Web;
using System.Web.Mvc;
using Umbraco.Web;

namespace Adventures.Widget
{
    public static class HtmlHelperExtensions
    {
        public static IHtmlString RenderOrder(this HtmlHelper htmlHelper, long order)
        {
            string path = "";

            return new HtmlString(
                htmlHelper.React(
                   "Search",
                   new
                   {
                       order = order,
                       umbracoDomainPath = path,
                   }).ToHtmlString()
               + $"<link rel=\"stylesheet\" href=\"/css/search.styles.css?v={DateTime.Now.Ticks}\" />"
               + $"<script src=\"http://localhost:8080/scripts/search.js\"></script>"
               + htmlHelper.ReactInitJavaScript().ToHtmlString());
        }
    }
}
