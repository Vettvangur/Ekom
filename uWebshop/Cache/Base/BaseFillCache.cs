using Examine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Cache.Base
{
    public class BaseFillCache
    {
        ICache _cache;
        ExamineManager _examineManager;
        Configuration _config;
        public BaseFillCache(ICache cache, ExamineManager examineManager, Configuration config)
        {
            _cache = cache;
            _examineManager = examineManager;
            _config = config;
        }

        public void FillCache()
        {
            var searcher = _examineManager.SearchProviderCollection[_config.ExamineSearcher];

            if (searcher != null && !string.IsNullOrEmpty(nodeAlias))
            {
                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                Log.Info("Starting to fill " + typeof(Tself).FullName + "...");

                var count = 0;

                try
                {
                    ISearchCriteria searchCriteria = searcher.CreateSearchCriteria();
                    var query = searchCriteria.NodeTypeAlias(nodeAlias);
                    var results = searcher.Search(query.Compile());

                    foreach (var r in results.Where(x => x.Fields["template"] != "0"))
                    {
                        // Traverse up parent nodes, checking only published status
                        if (!r.IsItemUnpublished())
                        {
                            var item = New(r);

                            if (item != null)
                            {
                                count++;
                                AddOrReplaceFromCache(r.Id, item);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("Filling Base Cache Failed!", ex);
                }

                stopwatch.Stop();

                Log.Info("Finished filling " + typeof(Tself).FullName + " with " + count + " items. Time it took to fill: " + stopwatch.Elapsed);
            }
            else
            {
                Log.Info("No examine search found with the name ExternalSearcher, Can not fill category cache.");
            }

        }
    }
}
