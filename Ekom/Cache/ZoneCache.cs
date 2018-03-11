using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;
using Umbraco.Core.Models;
using Ekom.Interfaces;

namespace Ekom.Cache
{
    class ZoneCache : BaseCache<IZone>
    {
        public override string NodeAlias { get; } = "ekmZone";

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        public ZoneCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager
        ) : base(config, examineManager, null)
        {
            _log = logFac.GetLogger<ZoneCache>();
        }
    }
}
