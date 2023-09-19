using Ekom.Models;
using Ekom.Services;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Trees;

namespace Ekom.Tree
{
    [SearchableTree("searchResultFormatter", "configureContentResult", 0)]
    class EkomSearchTree : ISearchableTree
    {
        public string TreeAlias => Umbraco.Cms.Core.Constants.Trees.Content;

        private readonly ICatalogSearchService _searchService;
        private readonly INodeService _nodeService;
        public EkomSearchTree(ICatalogSearchService searchService, INodeService nodeService)
        {
            _searchService = searchService;
            _nodeService = nodeService;
        }

        public Task<EntitySearchResults> SearchAsync(string query, int pageSize, long pageIndex, string? searchFrom = null)
        {
            long totalFound = 0;
            var searchResults = new List<Umbraco.Cms.Core.Models.ContentEditing.SearchResultEntity?>();
            if (!string.IsNullOrEmpty(query) && query.Length > 2)
            {
                var results = _searchService.InternalQuery(new SearchRequest() {
                    SearchQuery = query,
                    ExamineIndex = "InternalIndex"
                }, out _);

                foreach (var result in results.Take(30))
                {
                    var icon = "icon-document";
;
                    var alias = result.DocType;
                    var name = result.Name;
                    var url = result.Url;
                    if (alias == "ekmProduct")
                    {
                        var product = Ekom.API.Catalog.Instance.GetProduct(result.Id);

                        if (product != null)
                        {
                            url = product.Url;
                        }else
                        {
                            url = "Product not published or not found.";
                        }

                        icon = "icon-loupe";
                        name = result.Name + " (sku: " + result.SKU + ")";
                    } else if (alias == "ekmCategory")
                    {
                        var category = Ekom.API.Catalog.Instance.GetCategory(result.Id);

                        if (category != null)
                        {
                            url = category.Url;
                        } else
                        {
                            url = "Category not published or not found.";
                        }

                        icon = "icon-folder";
                    } else if (alias == "ekmProductVariant")
                    {
                        icon = "icon-layers-alt";
                    } else
                    {
                        var node = _nodeService.NodeById(result.Id);

                        if (node != null) {
                            url = node.Url;
                        } else
                        {
                            url = "Node not published or not found.";
                        }
                    }

                    var item = new Umbraco.Cms.Core.Models.ContentEditing.SearchResultEntity()
                    {
                        Name = name,
                        Id = result.Id,
                        Key = result.Key,
                        Score = result.Score,
                        Path = result.Path,
                        Icon = icon
                    };

                    if (!string.IsNullOrEmpty(url))
                    {
                        item.AdditionalData["Url"] = url;
                    }

                    searchResults.Add(item);
                }

            }

            return Task.FromResult(new EntitySearchResults(searchResults, totalFound));
        }
    }
}
