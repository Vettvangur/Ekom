using Ekom.Extensions.Controllers;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Umbraco.Web;

namespace Ekom.Extensions.Utilities
{
    /// <summary>
    /// MVC <see cref="HtmlHelper"/> extensions
    /// </summary>
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Render an Ekom form that submits to the <see cref="CheckoutController"/>
        /// Placing html inside using construct.
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="formType"></param>
        /// <param name="className">Override the default Ekom classnames</param>
        /// <returns></returns>
        /// <example>
        /// using (Html.BeginUmbracoForm(FormType.Pay)) 
        /// {
        ///     <input type="hidden" name="input" value="value" />
        ///     <button type = "submit" class="button">Submit</button>            
        /// }
        /// </example>
        public static MvcForm BeginEkomCheckoutForm(this HtmlHelper htmlHelper, CheckoutFormType formType, string className = null)
        {
            var actionName = formType.ToString();
            var defaultClassName = "cart__payment-form";

            switch (formType)
            {
                case CheckoutFormType.Pay:

                    break;

            }

            className = className ?? defaultClassName;
    
            return htmlHelper.BeginUmbracoForm<CheckoutController>(actionName, null, new { @class = className });
        }
    }

    /// <summary>
    /// Form controller action to render
    /// </summary>
    public enum CheckoutFormType
    {
        /// <summary>
        /// Complete payment using the Standard Ekom checkout controller
        /// </summary>
        Pay,
    }
}
