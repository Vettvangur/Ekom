using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Infrastructure.WebAssets;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Currency values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [DataEditor(
    "Ekom.Currency",
    EditorType.PropertyValue,
    "Ekom Currency Picker",
    "/App_Plugins/Ekom/DataTypes/CurrencyPicker/ekmCurrency.html",
    ValueType = ValueTypes.Json,
    HideLabel = true)]
    [PropertyEditorAsset(AssetType.Javascript, "/App_Plugins/Ekom/DataTypes/CurrencyPicker/ekmCurrency.controller.js")]
    public class EkomCurrencyPicker : DataEditor
    {
        public EkomCurrencyPicker(
            IDataValueEditorFactory dataValueEditorFactory,
            EditorType type = EditorType.PropertyValue)
            : base(dataValueEditorFactory, type)
        {
        }
    }


}
