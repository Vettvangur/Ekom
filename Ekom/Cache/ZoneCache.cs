using Examine;
using Ekom.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;
using Umbraco.Core.Models;

namespace Ekom.Cache
{
    class ZoneCache : BaseCache<Zone>
    {
        public override string NodeAlias { get; } = "ekmZone";

        protected override Zone New(SearchResult r)
        {
            return new Zone(r);
        }
        protected override Zone New(IContent content)
        {
            return new Zone(content);
        }

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
        )
        {
            _config = config;
            _examineManager = examineManager;
            _log = logFac.GetLogger<ZoneCache>();
        }
    }
}
