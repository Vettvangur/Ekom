using ClientDependency.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Coupon values.
    /// Calls Ekom.Api.InsertCoupon/RemoveCoupon/GetCouponsForDiscount for actions
    /// </summary>
    /// <seealso cref="DataEditor" />
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/CouponEditor/ekomCoupon.controller.js", Priority = 1)]
    [DataEditor("Ekom.Coupon", "Ekom Coupon Editor", "~/App_Plugins/Ekom/DataTypes/CouponEditor/ekomCoupon.html", ValueType = ValueTypes.Json)]
    public class EkomCouponEditor : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomCouponEditor"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomCouponEditor(ILogger logger)
            : base(logger)
        {
        }
    }
}
