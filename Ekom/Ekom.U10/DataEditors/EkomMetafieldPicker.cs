using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Metafield values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Metafield",
    EditorType.PropertyValue,
    "Ekom Metafield Picker",
    "/App_Plugins/Ekom/DataTypes/MetaFieldPicker/ekmMetafield.html",
    ValueType = ValueTypes.Json,
    HideLabel = false)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/MetaFieldPicker/ekmMetafield.controller.js")]
    public class EkomMetafieldPicker : DataEditor
    {
        public EkomMetafieldPicker(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }


}
