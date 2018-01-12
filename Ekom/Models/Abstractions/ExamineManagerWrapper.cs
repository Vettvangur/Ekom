using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Ekom.Models.Abstractions
{
    class ExamineManagerWrapper : ExamineManagerBase
    {
        private ExamineManager _examine;
        public ExamineManagerWrapper(ExamineManager examine)
        {
            _examine = examine;
        }

        public override IndexProviderCollection IndexProviderCollection => _examine.IndexProviderCollection;
        public override SearchProviderCollection SearchProviderCollection => _examine.SearchProviderCollection;
        public override BaseSearchProvider DefaultSearchProvider => _examine.DefaultSearchProvider;
        public override IIndexCriteria IndexerData
        {
            get => _examine.IndexerData;
            set => _examine.IndexerData = value;
        }

        public override ISearchCriteria CreateSearchCriteria(string type, BooleanOperation defaultOperation)
        {
            return _examine.CreateSearchCriteria(type, defaultOperation);
        }
        public override ISearchCriteria CreateSearchCriteria(BooleanOperation defaultOperation)
        {
            return _examine.CreateSearchCriteria(defaultOperation);
        }
        public override ISearchCriteria CreateSearchCriteria()
        {
            return _examine.CreateSearchCriteria();
        }
        public override ISearchCriteria CreateSearchCriteria(string type)
        {
            return _examine.CreateSearchCriteria(type);
        }
        public override void DeleteFromIndex(string nodeId)
        {
            _examine.DeleteFromIndex(nodeId);
        }
        public override void DeleteFromIndex(string nodeId, IEnumerable<BaseIndexProvider> providers)
        {
            _examine.DeleteFromIndex(nodeId, providers);
        }
        public override void IndexAll(string type)
        {
            _examine.IndexAll(type);
        }
        public override bool IndexExists()
        {
            return _examine.IndexExists();
        }
        public override void RebuildIndex()
        {
            _examine.RebuildIndex();
        }
        public override void ReIndexNode(XElement node, string type)
        {
            _examine.ReIndexNode(node, type);
        }
        public override void ReIndexNode(XElement node, string type, IEnumerable<BaseIndexProvider> providers)
        {
            _examine.ReIndexNode(node, type, providers);
        }
        public override ISearchResults Search(string searchText, bool useWildcards)
        {
            return _examine.Search(searchText, useWildcards);
        }
        public override ISearchResults Search(ISearchCriteria searchParameters)
        {
            return _examine.Search(searchParameters);
        }
        public override void Stop(bool immediate)
        {
            _examine.Stop(immediate);
        }
    }
}
