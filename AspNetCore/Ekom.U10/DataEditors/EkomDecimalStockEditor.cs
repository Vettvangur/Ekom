using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Decimal Stock values.
    /// Calls Ekom/Api/GetStockByStore to query/update stock.
    /// Supports decimal stock values for products sold in non-whole numbers.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.DecimalStock",
    EditorType.PropertyValue,
    "Ekom Decimal Stock Editor",
    "/App_Plugins/Ekom/DataTypes/DecimalStockEditor/ekomDecimalStock.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/DecimalStockEditor/ekomDecimalStock.controller.js")]
    public class EkomDecimalStockEditor : DataEditor
    {
        public EkomDecimalStockEditor(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }
}
