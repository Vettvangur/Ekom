using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Ekom.Utilities
{
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Render an Ekom form that submits to the <see cref="OrderController"/>
        /// Placing html inside using construct.
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="formType"></param>
        /// <param name="className">Override the default Ekom classnames</param>
        /// <param name="Id"></param>
        /// <param name="htmlAttributes"></param>
        /// <returns></returns>
        /// <example>
        /// using (Html.BeginEkomForm(FormType.UpdatePaymentProvider)) 
        /// {
        ///     <input type="hidden" name="input" value="value" />
        ///     <button type = "submit" class="button">Submit</button>            
        /// }
        /// </example>
        public static MvcForm BeginEkomForm(this IHtmlHelper htmlHelper, FormType formType, string className = null, string Id = null, Dictionary<string,object> htmlAttributes = null)
        {
            var actionName = formType.ToString();
            var defaultClassName = "";

            switch (formType)
            {
                case FormType.AddToOrderProduct:

                    actionName = "AddToOrder";
                    defaultClassName = "product__form";
                    break;

                case FormType.AddToOrderCart:

                    actionName = "AddToOrder";
                    defaultClassName = "cart__quantity-form";
                    break;
                case FormType.RemoveOrderLine:

                    defaultClassName = "cart__remove-form";
                    break;
                case FormType.UpdateOrderLineQuantity:

                    defaultClassName = "cart__update-quantity-form";
                    break;
                case FormType.UpdatePaymentProvider:

                    defaultClassName = "cart__payment-form";
                    break;

                case FormType.UpdateShippingProvider:

                    defaultClassName = "cart__shipping-form";
                    break;

                case FormType.UpdateCustomerInformation:

                    defaultClassName = "cart__quantity-form";
                    break;
                case FormType.ApplyCouponToOrder:

                    defaultClassName = "cart__coupon-form";
                    break;
                case FormType.ChangeCurrency:

                    defaultClassName = "order__changeCurrency-form";
                    break;
            }

            className = className ?? defaultClassName;
            
            var form = htmlHelper.BeginForm(actionName, "EkomOrder", null, FormMethod.Post, true, htmlAttributes: htmlAttributes != null ? htmlAttributes : new { @class = className, @id = Id });
            
            return form;
        }

        /// <summary>
        /// Render an Ekom checkout form that submits to the <see cref="EkomCheckoutController"/>
        /// Placing html inside using construct.
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="formType"></param>
        /// <param name="className">Override the default Ekom classnames</param>
        /// <returns></returns>
        /// <example>
        /// using (Html.BeginEkomCheckoutForm(CheckoutFormType.Pay)) 
        /// {
        ///     <input type="hidden" name="input" value="value" />
        ///     <button type = "submit" class="button">Submit</button>            
        /// }
        /// </example>
        public static MvcForm BeginEkomCheckoutForm(this IHtmlHelper htmlHelper, CheckoutFormType formType, string className = null, string Id = null, Dictionary<string, object> htmlAttributes = null)

        {
            var actionName = formType.ToString();
            var defaultClassName = "";

            switch (formType)
            {
                case CheckoutFormType.Pay:

                    actionName = "Pay";
                    defaultClassName = "checkout__form";
                    break;
            }

            className = className ?? defaultClassName;

            var form = htmlHelper.BeginForm(actionName, "Checkout", new { }, FormMethod.Post, true, htmlAttributes: htmlAttributes != null ? htmlAttributes : new { @class = className, @id = Id });

            htmlHelper.ViewContext.Writer.WriteLine($"<input type='hidden' name='returnUrl' value='{htmlHelper.ViewContext.HttpContext.Request.GetDisplayUrl()}' />");
            
            return form;
        }
    }
}

