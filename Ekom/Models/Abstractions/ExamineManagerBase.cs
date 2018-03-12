using Examine;
using Examine.Providers;
using Examine.SearchCriteria;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Ekom.Models.Abstractions
{
    public abstract class ExamineManagerBase
    {
        public abstract IndexProviderCollection IndexProviderCollection { get; }
        public abstract SearchProviderCollection SearchProviderCollection { get; }
        public abstract BaseSearchProvider DefaultSearchProvider { get; }
        public abstract IIndexCriteria IndexerData { get; set; }

        public abstract ISearchCriteria CreateSearchCriteria(string type, BooleanOperation defaultOperation);
        public abstract ISearchCriteria CreateSearchCriteria(BooleanOperation defaultOperation);
        public abstract ISearchCriteria CreateSearchCriteria();
        public abstract ISearchCriteria CreateSearchCriteria(string type);
        public abstract void DeleteFromIndex(string nodeId);
        public abstract void DeleteFromIndex(string nodeId, IEnumerable<BaseIndexProvider> providers);
        public abstract void IndexAll(string type);
        public abstract bool IndexExists();
        public abstract void RebuildIndex();
        public abstract void ReIndexNode(XElement node, string type);
        public abstract void ReIndexNode(XElement node, string type, IEnumerable<BaseIndexProvider> providers);
        public abstract ISearchResults Search(string searchText, bool useWildcards);
        public abstract ISearchResults Search(ISearchCriteria searchParameters);
        public abstract void Stop(bool immediate);
    }
}
