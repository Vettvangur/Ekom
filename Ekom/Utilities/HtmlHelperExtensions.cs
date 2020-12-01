using Ekom.Controllers;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using Umbraco.Web;

namespace Ekom.Utilities
{
    /// <summary>
    /// MVC <see cref="HtmlHelper"/> extensions
    /// </summary>
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// Render an Ekom form that submits to the <see cref="OrderController"/>
        /// Placing html inside using construct.
        /// </summary>
        /// <param name="htmlHelper"></param>
        /// <param name="formType"></param>
        /// <param name="className">Override the default Ekom classnames</param>
        /// <returns></returns>
        /// <example>
        /// using (Html.BeginEkomForm(FormType.UpdatePaymentProvider)) 
        /// {
        ///     <input type="hidden" name="input" value="value" />
        ///     <button type = "submit" class="button">Submit</button>            
        /// }
        /// </example>
        public static MvcForm BeginEkomForm(this HtmlHelper htmlHelper, FormType formType, string className = null, string Id = null)
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
                case FormType.UpdateOrderLineQuantity:

                    actionName = "UpdateOrderlineQuantity";
                    defaultClassName = "cart__quantity-form";
                    break;
                case FormType.RemoveOrderLine:

                    defaultClassName = "cart__remove-form";
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

            return htmlHelper.BeginUmbracoForm<OrderController>(actionName, null, new { @class = className, @id = Id });
        }
    }

    /// <summary>
    /// Form controller action to render
    /// </summary>
    public enum FormType
    {
        /// <summary>
        /// AddOrderLine action with product page class
        /// </summary>
        AddToOrderProduct,
        /// <summary>
        /// AddOrderLine action with cart view styles
        /// </summary>
        AddToOrderCart,
        /// <summary>
        /// Update orderline quantity
        /// </summary>
        UpdateOrderLineQuantity,
        /// <summary>
        /// 
        /// </summary>
        RemoveOrderLine,
        /// <summary>
        /// 
        /// </summary>
        UpdatePaymentProvider,
        /// <summary>
        /// 
        /// </summary>
        UpdateShippingProvider,
        /// <summary>
        /// 
        /// </summary>
        UpdateCustomerInformation,
        /// <summary>
        /// 
        /// </summary>
        ApplyCouponToOrder,
        /// <summary>
        /// 
        /// </summary>
        ChangeCurrency
    }
}
