using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Coupon values.
    /// Calls Ekom.Api.InsertCoupon/RemoveCoupon/GetCouponsForDiscount for actions
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Coupon",
    EditorType.PropertyValue,
    "Ekom Coupon Editor",
    "/App_Plugins/Ekom/DataTypes/CouponEditor/ekomCoupon.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/CouponEditor/ekomCoupon.controller.js")]
    public class EkomCouponEditor : DataEditor
    {
        public EkomCouponEditor(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }
}
