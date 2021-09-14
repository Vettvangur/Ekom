using Examine;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Quick and easy examine querying
    /// </summary>
    public interface IExamineService
    {
        ISearchResult GetExamineNode(int Id);
        ISearchResult GetExamineNode(string Key);
        /// <summary>
        /// Intended for Ekom library users, will have more Ekom specific functionality later on
        /// Builds and runS the Lucene query.
        /// </summary>
        ISearchResults SearchResult(string query, string examineIndex, out long total);

        void Rebuild();
    }
}
