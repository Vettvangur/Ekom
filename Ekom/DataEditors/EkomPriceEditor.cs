using ClientDependency.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Price values.
    /// </summary>
    /// <seealso cref="DataEditor" />
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/PriceEditor/ekmPrice.controller.js", Priority = 1)]
    [DataEditor("Ekom.Price", "Ekom Price Editor", "~/App_Plugins/Ekom/DataTypes/PriceEditor/ekmPrice.html", ValueType = ValueTypes.Json)]
    public class EkomPriceEditor : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomPriceEditor"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomPriceEditor(ILogger logger)
            : base(logger)
        {
        }
    }
}
