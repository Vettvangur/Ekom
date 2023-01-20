using Examine;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Infrastructure.Examine;

namespace Ekom.Umb.Indexers
{
    public class EkomIndexComponent : IComponent
    {
        private readonly IExamineManager _examineManager;
        private readonly EkomIndexCreator _productIndexCreator;
        private readonly Configuration _config;
        public EkomIndexComponent(IExamineManager examineManager, EkomIndexCreator productIndexCreator, Configuration config)
        {
            _examineManager = examineManager;
            _productIndexCreator = productIndexCreator;
            _config = config;
        }

        public void Initialize()
        {
            //    if (_config.CustomIndex)
            //    {
            //        foreach (var index in _productIndexCreator.Create())
            //        {
            //            ((BaseIndexProvider)index).TransformingIndexValues += IndexerComponent_TransformingIndexValues;

            //            _examineManager.GetIndex(index.ToString());
            //        }
            //    }
            //}
            //private void IndexerComponent_TransformingIndexValues(object sender, IndexingItemEventArgs e)
            //{
            //    if (e.ValueSet.Category == IndexTypes.Content)
            //    {
            //        string searchablePath = "";
            //        foreach (var fieldValues in e.ValueSet.Values)
            //        {
            //            if (fieldValues.Key == "path")
            //            {
            //                foreach (var value in fieldValues.Value)
            //                {
            //                    var path = value.ToString().Replace(",", " ");

            //                    searchablePath = string.Join(" ", path.Split(',').Select(x => string.Format("{1}{0}{1}", x.Replace(" ", "|").ToLower(), '|')));
            //                }
            //            }
            //        }

            //        e.ValueSet.TryAdd("searchPath", searchablePath);
            //    }
        }

        public void Terminate()
        {
            //    foreach (var index in _examineManager.Indexes)
            //    {
            //        if (!(index is UmbracoExamineIndex umbracoIndex))
            //        {
            //            continue;
            //        }

            //        ((BaseIndexProvider)index).TransformingIndexValues -= IndexerComponent_TransformingIndexValues;

            //    }
        }
    }
}
