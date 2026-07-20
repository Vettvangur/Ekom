namespace Ekom.Umb.CatalogCollection.Models;

public sealed class CatalogCollectionRequest
{
    public string Query { get; set; } = string.Empty;
    public string Sort { get; set; } = "sortOrderAsc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 80;
}

public sealed class CatalogCollectionResponse
{
    public CatalogCollectionNode Current { get; set; } = new();
    public CatalogCollectionNode? Parent { get; set; }
    public IReadOnlyList<CatalogCollectionBreadcrumb> Breadcrumbs { get; set; } = Array.Empty<CatalogCollectionBreadcrumb>();
    public IReadOnlyList<CatalogCollectionNode> Subcategories { get; set; } = Array.Empty<CatalogCollectionNode>();
    public IReadOnlyList<CatalogCollectionProduct> Products { get; set; } = Array.Empty<CatalogCollectionProduct>();
    public int ProductCount { get; set; }
    public int SubcategoryCount { get; set; }
    public int FilteredProductCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class CatalogCollectionNode
{
    public int Id { get; set; }
    public Guid Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ContentTypeAlias { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int ProductCount { get; set; }
    public int SubcategoryCount { get; set; }
}

public sealed class CatalogCollectionBreadcrumb : CatalogCollectionNode
{
    public IReadOnlyList<CatalogCollectionNode> Siblings { get; set; } = Array.Empty<CatalogCollectionNode>();
}

public sealed class CatalogCollectionProduct : CatalogCollectionNode
{
    public string Sku { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public decimal? PriceValue { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool Published { get; set; }
    public bool PendingChanges { get; set; }
    public bool Available { get; set; }
    public string Image { get; set; } = string.Empty;
}
