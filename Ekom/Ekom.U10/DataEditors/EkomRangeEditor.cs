using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Range values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Range",
    EditorType.PropertyValue,
    "Ekom Range Editor",
    "/App_Plugins/Ekom/DataTypes/RangeEditor/ekmRange.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/RangeEditor/ekmRange.controller.js")]
    public class EkomRangeEditor : DataEditor
    {
        public EkomRangeEditor(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }


}
