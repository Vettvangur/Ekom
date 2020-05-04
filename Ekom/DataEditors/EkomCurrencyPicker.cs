using ClientDependency.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Currency values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/CurrencyPicker/ekmCurrency.controller.js", Priority = 1)]
    [DataEditor("Ekom.Currency", "Ekom Currency Picker", "~/App_Plugins/Ekom/DataTypes/CurrencyPicker/ekmCurrency.html", ValueType = ValueTypes.Json)]
    public class EkomCurrencyPicker : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomCurrencyPicker"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomCurrencyPicker(ILogger logger)
            : base(logger)
        {
        }
    }
}
