using System.Globalization;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Ekom.Umb.CatalogCollection.Services;

/// <summary>
/// Reads the narrow database rows the catalog view is built from.
/// </summary>
internal sealed class CatalogCollectionRowReader
{
    private readonly IScopeProvider _scopeProvider;

    public CatalogCollectionRowReader(IScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private const int SqlInChunkSize = 1000;

    private const string ProductRowSelect = @"SELECT n.id AS Id, n.uniqueId AS UniqueId, n.parentId AS ParentId, n.path AS Path,
       n.sortOrder AS SortOrder, n.text AS Name, n.createDate AS CreateDate,
       cv.versionDate AS UpdateDate, d.published AS Published
FROM umbracoNode n
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
INNER JOIN umbracoDocument d ON d.nodeId = n.id
INNER JOIN umbracoContentVersion cv ON cv.nodeId = n.id AND cv.{0} = 1
WHERE n.trashed = 0
  AND ct.alias = 'ekmProduct'
  AND ";

    public List<CatalogProductRow> GetProductRows(string parentPath)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var current = scope.SqlContext.SqlSyntax.GetQuotedColumnName("current");

        return scope.Database.Fetch<CatalogProductRow>(
            string.Format(CultureInfo.InvariantCulture, ProductRowSelect, current) + "n.path LIKE @0",
            parentPath + ",%");
    }

    /// <summary>
    /// Products connected to the node, or to a category below it, through their categories picker.
    /// Umbraco tracks every picked document as a relation when the product is saved, so this is
    /// an indexed lookup rather than a scan of property values.
    /// </summary>
    public List<CatalogLinkRow> GetLinkRows(int nodeId, string nodePath)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);

        return scope.Database.Fetch<CatalogLinkRow>(
            @"SELECT r.parentId AS ProductId, r.childId AS CategoryId, cn.path AS CategoryPath
FROM umbracoRelation r
INNER JOIN umbracoRelationType rt ON rt.id = r.relType
INNER JOIN umbracoNode cn ON cn.id = r.childId
INNER JOIN umbracoNode pn ON pn.id = r.parentId
INNER JOIN umbracoContent pc ON pc.nodeId = pn.id
INNER JOIN cmsContentType pct ON pct.nodeId = pc.contentTypeId
WHERE rt.alias = @0
  AND (cn.id = @1 OR cn.path LIKE @2)
  AND cn.trashed = 0
  AND pn.trashed = 0
  AND pct.alias = 'ekmProduct'",
            Constants.Conventions.RelationTypes.RelatedDocumentAlias,
            nodeId,
            nodePath + ",%");
    }

    /// <summary>
    /// Adds the rows of linked products that live outside the node's subtree, with their variants.
    /// </summary>
    public void AddLinkedProducts(List<CatalogProductRow> products, List<CatalogVariantRow> variants, List<CatalogLinkRow> links)
    {
        if (links.Count == 0)
        {
            return;
        }

        var known = products.Select(x => x.Id).ToHashSet();
        var missing = links.Select(x => x.ProductId).Where(x => !known.Contains(x)).Distinct().ToList();
        if (missing.Count == 0)
        {
            return;
        }

        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var current = scope.SqlContext.SqlSyntax.GetQuotedColumnName("current");
        var productSql = string.Format(CultureInfo.InvariantCulture, ProductRowSelect, current) + "n.id IN (@0)";

        foreach (var chunk in missing.Chunk(SqlInChunkSize))
        {
            products.AddRange(scope.Database.Fetch<CatalogProductRow>(productSql, chunk));
            variants.AddRange(scope.Database.Fetch<CatalogVariantRow>(
                @"SELECT n.uniqueId AS UniqueId, n.path AS Path
FROM umbracoNode n
INNER JOIN umbracoNode g ON g.id = n.parentId
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
WHERE g.parentId IN (@0)
  AND n.trashed = 0
  AND ct.alias = 'ekmProductVariant'",
                chunk));
        }
    }

    public List<CatalogVariantRow> GetVariantRows(string parentPath)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);

        return scope.Database.Fetch<CatalogVariantRow>(
            @"SELECT n.uniqueId AS UniqueId, n.path AS Path
FROM umbracoNode n
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
WHERE n.path LIKE @0
  AND n.trashed = 0
  AND ct.alias = 'ekmProductVariant'",
            parentPath + ",%");
    }

    /// <summary>
    /// Recursive product totals for every category below the given path, from two aggregate queries.
    /// </summary>
    public IReadOnlyDictionary<int, int> GetCategoryTotals(string scopePath)
    {
        using var scope = _scopeProvider.CreateScope(autoComplete: true);
        var productCounts = scope.Database.Fetch<CatalogParentCountRow>(
            @"SELECT n.parentId AS ParentId, COUNT(*) AS Count
FROM umbracoNode n
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
WHERE n.path LIKE @0
  AND n.trashed = 0
  AND ct.alias = 'ekmProduct'
GROUP BY n.parentId",
            scopePath + ",%");
        var categories = scope.Database.Fetch<CatalogCategoryRow>(
            @"SELECT n.id AS Id, n.parentId AS ParentId
FROM umbracoNode n
INNER JOIN umbracoContent c ON c.nodeId = n.id
INNER JOIN cmsContentType ct ON ct.nodeId = c.contentTypeId
WHERE n.path LIKE @0
  AND n.trashed = 0
  AND ct.alias = 'ekmCategory'",
            scopePath + ",%");

        return CatalogCollectionIndex.GetRecursiveTotals(
            categories.Select(x => (x.Id, x.ParentId)),
            productCounts.ToDictionary(x => x.ParentId, x => x.Count));
    }

    private sealed class CatalogParentCountRow
    {
        public int ParentId { get; set; }
        public int Count { get; set; }
    }

    private sealed class CatalogCategoryRow
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }
}
