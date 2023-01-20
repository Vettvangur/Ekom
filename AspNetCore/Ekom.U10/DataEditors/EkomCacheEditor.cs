using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.Umb.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Cache Editor.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Cache",
    EditorType.PropertyValue,
    "Ekom Cache Editor",
    "/App_Plugins/Ekom/DataTypes/CacheEditor/ekmCache.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/CacheEditor/ekmCache.controller.js")]
    public class EkomCacheEditor : DataEditor
    {
        public EkomCacheEditor(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }




}
