using Ekom.Services;
using Ekom.Umb.Services;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Trees;

namespace Ekom.Tree
{
    [SearchableTree("searchResultFormatter", "configureContentResult", 0)]
    class EkomSearchTree : ISearchableTree
    {
        public string TreeAlias => Umbraco.Cms.Core.Constants.Trees.Content;

        private readonly ICatalogSearchService _searchService;
        public EkomSearchTree(ICatalogSearchService searchService)
        {
            _searchService = searchService;
        }

        public Task<EntitySearchResults> SearchAsync(string query, int pageSize, long pageIndex, string? searchFrom = null)
        {
            long totalFound = 0;
            var searchResults = new List<SearchResultEntity?>();
            if (!string.IsNullOrEmpty(query) && query.Length > 2)
            {
                var results = _searchService.QueryCatalog(query, out totalFound);

                foreach (var result in results)
                {
                    var icon = "icon-document";
;
                    var alias = result.DocType;
                    var name = result.Name;

                    if (alias == "ekmProduct")
                    {
                        icon = "icon-loupe";
                        name = result.Name + " (" + result.SKU + ")" + " (" + result.ParentName + ")";
                    } else if (alias == "ekmCategory")
                    {
                        icon = "icon-folder";
                    } else if (alias == "ekmProductVariant")
                    {
                        icon = "icon-layers-alt";
                    }

                    var item = new SearchResultEntity()
                    {
                        Name = name,
                        Id = result.Id,
                        Key = result.Key,
                        Score = result.Score,
                        Path = result.Path,
                        Icon = icon
                    };

                    item.AdditionalData["Url"] = result.Url;

                    searchResults.Add(item);
                }

            }

            return Task.FromResult(new EntitySearchResults(searchResults, totalFound));
        }
    }
}
