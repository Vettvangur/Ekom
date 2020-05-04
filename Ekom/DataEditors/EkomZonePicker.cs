using ClientDependency.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Zone values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/ZonePicker/ekmZone.controller.js", Priority = 1)]
    [DataEditor("Ekom.Zone", "Ekom Zone Picker", "~/App_Plugins/Ekom/DataTypes/ZonePicker/ekmZone.html", ValueType = ValueTypes.Json)]
    public class EkomZonePicker : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomZonePicker"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomZonePicker(ILogger logger)
            : base(logger)
        {
        }
    }
}
