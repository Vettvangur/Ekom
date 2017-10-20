using Examine;
using Ekom.Models;
using Ekom.Services;

namespace Ekom.Cache
{
    class ZoneCache : BaseCache<Zone>
    {
        public override string NodeAlias { get; } = "uwbsZone";

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        public ZoneCache(
            ILogFactory logFac,
            Configuration config,
            ExamineManager examineManager
        )
        {
            _config = config;
            _examineManager = examineManager;
            _log = logFac.GetLogger(typeof(ZoneCache));
        }

        protected override Zone New(SearchResult r)
        {
            return new Zone(r);
        }
    }
}
