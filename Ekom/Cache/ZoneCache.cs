using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System;

namespace Ekom.Cache
{
    class ZoneCache : BaseCache<IZone>
    {
        public override string NodeAlias { get; } = "ekmZone";

        /// <summary>
        /// ctor
        /// </summary>
        public ZoneCache(
            Configuration config,
            ILogger<BaseCache<IZone>> logger,
            IObjectFactory<IZone> objectFactory,
            IServiceProvider serviceProvider
        ) : base(config, logger, objectFactory, serviceProvider)
        {
        }
    }
}
