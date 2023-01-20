using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Stock values.
    /// Calls Ekom/Api/GetStockByStore to query/update stock
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Stock",
    EditorType.PropertyValue,
    "Ekom Stock Editor",
    "/App_Plugins/Ekom/DataTypes/StockEditor/ekomStock.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/StockEditor/ekomStock.controller.js")]
    public class EkomStockEditor : DataEditor
    {
        public EkomStockEditor(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }
}
