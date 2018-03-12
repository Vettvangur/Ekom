using Ekom.Interfaces;
using Ekom.Services;

namespace Ekom.Cache
{
    class ZoneCache : BaseCache<IZone>
    {
        public override string NodeAlias { get; } = "ekmZone";

        /// <summary>
        /// ctor
        /// </summary>
        public ZoneCache(
            ILogFactory logFac,
            Configuration config
        ) : base(config, null)
        {
            _log = logFac.GetLogger<ZoneCache>();
        }
    }
}
