using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Price values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Price",
    EditorType.PropertyValue,
    "Ekom Price Editor",
    "/App_Plugins/Ekom/DataTypes/PriceEditor/ekmPrice.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/PriceEditor/ekmPrice.controller.js")]
    public class EkomPriceEditor : DataEditor
    {
        public EkomPriceEditor(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }


}
