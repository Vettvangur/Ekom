using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.Umb.DataEditors
{
    [DataEditor(
    "Ekom.Property",
    EditorType.PropertyValue,
    "Ekom Property Editor",
    "/App_Plugins/Ekom/DataTypes/PropertyEditor/views/ekmPropertyEditor.html",
    ValueType = ValueTypes.Json,
    HideLabel = false)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/PropertyEditor/js/ekmPropertyEditor.controller.js")]
    [PropertyEditorAsset(AssetType.Css, "/App_Plugins/Ekom/DataTypes/PropertyEditor/css/ekomProperty.css")]
    public class EkomPropertyEditor : DataEditor
    {
        private readonly IIOHelper _ioHelper;
        private readonly IEditorConfigurationParser _editorConfigurationParser;

        public EkomPropertyEditor(
            IDataValueEditorFactory dataValueEditorFactory,
            IIOHelper ioHelper,
            IEditorConfigurationParser editorConfigurationParser,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
            _editorConfigurationParser = editorConfigurationParser;
            _ioHelper = ioHelper;
        }

        protected override IConfigurationEditor CreateConfigurationEditor() => new EkomPropertyEditorConfigurationEditor(_ioHelper, _editorConfigurationParser);
    }

    public class EkomPropertyEditorConfigurationEditor : ConfigurationEditor<EkomPropertyEditorConfiguration>
    {
        public EkomPropertyEditorConfigurationEditor(IIOHelper ioHelper, IEditorConfigurationParser editorConfigurationParser) : base(ioHelper, editorConfigurationParser)
        {
        }
    }

    public class EkomPropertyEditorConfiguration
    {
        [ConfigurationField(
            "dataType", 
            "Data Type",
            "/App_Plugins/Ekom/DataTypes/PropertyEditor/views/ekmPropertyEditorPicker.html",
            Description =
                "Select the data type to wrap.")]
        public object DataType { get; set; }


        [ConfigurationField(
        "useLanguages",
        "Use Languages",
        "boolean",
        Description =
            "Defaults on Stores, select this to use languages instead")]
        public bool useLanguages { get; set; }

        [ConfigurationField(
            "hideLabel", 
            "Hide Label", 
            "boolean", 
            Description = "Hide the Umbraco property title and description, making the property span the entire page width")]
        public bool HideLabel { get; set; }
    }
}
