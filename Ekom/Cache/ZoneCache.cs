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
            ILogger logger,
            Configuration config,
            IObjectFactory<IZone> objectFactory
        ) : base(config, objectFactory)
        {
            _logger = logger;
        }
    }
}
