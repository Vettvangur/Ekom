using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.Umb.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Zone values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Zone",
    EditorType.PropertyValue,
    "Ekom Zone Picker",
    "/App_Plugins/Ekom/DataTypes/ZonePicker/ekmZone.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/ZonePicker/ekmZone.controller.js")]
    public class EkomZonePicker : DataEditor
    {
        public EkomZonePicker(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }




}
