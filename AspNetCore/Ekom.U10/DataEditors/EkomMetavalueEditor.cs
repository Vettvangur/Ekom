using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Meta values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Metavalue",
    EditorType.PropertyValue,
    "Ekom Metavalue Editor",
    "/App_Plugins/Ekom/DataTypes/MetaValueEditor/ekmMetavalue.html",
    ValueType = ValueTypes.Json,
    HideLabel = false)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/MetaValueEditor/ekmMetavalue.controller.js")]
    public class EkomMetavaluePicker : DataEditor
    {
        public EkomMetavaluePicker(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }


}
