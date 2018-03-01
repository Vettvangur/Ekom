using Examine;
using System.Diagnostics;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Models;
using Ekom.Services;
using Ekom.Models.Abstractions;

namespace Ekom.Cache
{
    class StoreDomainCache : BaseCache<IDomain>
    {
        public override string NodeAlias { get; } = "";

        ApplicationContext _appCtx;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="appCtx"></param>
        /// <param name="logFac"></param>
        /// <param name="config"></param>
        /// <param name="examineManager"></param>
        public StoreDomainCache(
            ApplicationContext appCtx,
            ILogFactory logFac,
            Configuration config,
            ExamineManagerBase examineManager
        )
        {
            _config = config;
            _examineManager = examineManager;
            _appCtx = appCtx;
            _log = logFac.GetLogger(typeof(StoreDomainCache));
        }

        /// <summary>
        /// Fill store domain cache with domains from domain service
        /// </summary>
        public override void FillCache()
        {
            var ds = _appCtx.Services.DomainService;

            var domains = ds.GetAll(false);

            if (domains.Any())
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                _log.Info("Starting to fill store domain cache...");

                foreach (var d in domains)
                {
                    AddOrReplaceFromCache(d.Key, d);
                }

                _log.Info("Finished filling store domain cache with " + domains.Count() + " domain items. Time it took to fill: " + stopwatch.Elapsed);
            }
        }

        protected override IDomain New(SearchResult r)
        {
            throw new System.NotImplementedException();
        }

        protected override IDomain New(IContent r)
        {
            throw new System.NotImplementedException();
        }
    }
}
