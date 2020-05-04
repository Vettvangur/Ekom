using ClientDependency.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Country values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/CountryPicker/ekmCountry.controller.js", Priority = 1)]
    [DataEditor("Ekom.Country", "Ekom Country Picker", "~/App_Plugins/Ekom/DataTypes/CountryPicker/ekmCountry.html", ValueType = ValueTypes.Json)]
    public class EkomCountryPicker : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomCountryPicker"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomCountryPicker(ILogger logger)
            : base(logger)
        {
        }
    }
}
