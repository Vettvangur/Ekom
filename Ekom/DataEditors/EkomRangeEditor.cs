using ClientDependency.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Range values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/RangeEditor/ekmRange.controller.js", Priority = 1)]
    [DataEditor("Ekom.Range", "Ekom Range Editor", "~/App_Plugins/Ekom/DataTypes/RangeEditor/ekmRange.html", ValueType = ValueTypes.Json)]
    public class EkomRangeEditor : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomRangeEditor"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomRangeEditor(ILogger logger)
            : base(logger)
        {
        }
    }
}
