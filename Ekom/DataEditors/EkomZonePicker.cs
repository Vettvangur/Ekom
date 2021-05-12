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
    [PropertyEditorAsset(ClientDependencyType.Javascript, "~/App_Plugins/Ekom/DataTypes/CacheEditor/ekmCache.controller.js", Priority = 1)]
    [DataEditor("Ekom.Cache", "Ekom Cache Editor", "~/App_Plugins/Ekom/DataTypes/CacheEditor/ekmCache.html", ValueType = ValueTypes.Json)]
    public class EkomCacheEditor : DataEditor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EkomCacheEditor"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public EkomCacheEditor(ILogger logger)
            : base(logger)
        {
        }
    }
}
