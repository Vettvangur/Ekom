using ClientDependency.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web.PropertyEditors;

namespace Ekom.DataEditors
{
    /// <summary>
    /// Umbraco Data Editor for Stock values.
    /// Calls Ekom/Api/GetStockByStore to query/update stock
    /// </summary>
    /// <seealso cref="DataEditor" />
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/StockEditor/ekomStock.controller.js", Priority = 1)]
    [DataEditor("Ekom.Stock", "Ekom Stock Editor", "~/App_Plugins/Ekom/DataTypes/StockEditor/ekomStock.html", ValueType = ValueTypes.Integer)]
    public class EkomStockEditor : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomStockEditor"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomStockEditor(ILogger logger)
            : base(logger)
        {
        }
    }
}
