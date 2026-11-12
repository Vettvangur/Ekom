using Ekom.Umb.CatalogCollection.Models;
using System.Globalization;

namespace Ekom.Umb.CatalogCollection.Services;

internal sealed class CatalogProductRow
{
    public int Id { get; set; }
    public Guid UniqueId { get; set; }
    public int ParentId { get; set; }
    public string Path { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Name { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime UpdateDate { get; set; }
    public bool Published { get; set; }
}

internal sealed class CatalogVariantRow
{
    public Guid UniqueId { get; set; }
    public string Path { get; set; } = string.Empty;
}

internal sealed class CatalogLinkRow
{
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public string CategoryPath { get; set; } = string.Empty;
}

internal sealed class CatalogProductEntry
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public DateTime CreateDate { get; init; }
    public DateTime UpdateDate { get; init; }
    public bool Published { get; init; }
    public int VariantCount { get; init; }
    public decimal Stock { get; init; }

    /// <summary>Shown through the product's categories picker; the node lives elsewhere.</summary>
    public bool Linked { get; init; }
}

internal sealed class CatalogBranchSummary
{
    public int Total { get; set; }
    public int Live { get; set; }
    public int OutOfStock { get; set; }
}

/// <summary>
/// In-memory view of the products below one catalog node, built from narrow database rows.
/// Answers counts, status filters and ordering without loading content.
/// </summary>
internal sealed class CatalogCollectionIndex
{
    private readonly Dictionary<int, CatalogBranchSummary> _branches;

    private CatalogCollectionIndex(
        IReadOnlyList<CatalogProductEntry> directProducts,
        Dictionary<int, CatalogBranchSummary> branches,
        int totalProductCount)
    {
        DirectProducts = directProducts;
        _branches = branches;
        TotalProductCount = totalProductCount;
    }

    /// <summary>Products that are direct children of the node.</summary>
    public IReadOnlyList<CatalogProductEntry> DirectProducts { get; }

    /// <summary>All products below the node, at any depth.</summary>
    public int TotalProductCount { get; }

    /// <summary>Summary of everything below one direct child of the node.</summary>
    public CatalogBranchSummary GetBranch(int childId)
        => _branches.TryGetValue(childId, out var branch) ? branch : new CatalogBranchSummary();

    /// <param name="nodeId">The category being viewed.</param>
    /// <param name="nodePath">Its Umbraco path.</param>
    /// <param name="products">Products below the node, plus any linked products living elsewhere.</param>
    /// <param name="variants">Variants of those products.</param>
    /// <param name="links">Categories picker links pointing at the node or a category below it.</param>
    /// <param name="getStock">Stock lookup by node key.</param>
    public static CatalogCollectionIndex Build(
        int nodeId,
        string nodePath,
        IReadOnlyList<CatalogProductRow> products,
        IReadOnlyList<CatalogVariantRow> variants,
        IReadOnlyList<CatalogLinkRow> links,
        Func<Guid, decimal> getStock)
    {
        var rows = new Dictionary<int, CatalogProductRow>(products.Count);
        foreach (var product in products)
        {
            rows.TryAdd(product.Id, product);
        }

        var productIds = new HashSet<int>(rows.Keys);
        var variantCounts = new Dictionary<int, int>();
        var variantStock = new Dictionary<int, decimal>();
        var seenVariants = new HashSet<Guid>();
        foreach (var variant in variants)
        {
            var productId = FindOwner(variant.Path, productIds);
            if (productId == 0 || !seenVariants.Add(variant.UniqueId))
            {
                continue;
            }

            variantCounts[productId] = variantCounts.GetValueOrDefault(productId) + 1;
            variantStock[productId] = variantStock.GetValueOrDefault(productId) + Math.Max(0, getStock(variant.UniqueId));
        }

        // The child of the node on a descendant's path sits right after the node's own path.
        var childOffset = nodePath.Length + 1;
        var subtreePrefix = nodePath + ",";
        var stock = new Dictionary<int, decimal>(rows.Count);
        var direct = new Dictionary<int, CatalogProductEntry>();
        var branches = new Dictionary<int, CatalogBranchSummary>();
        var placed = new HashSet<(int BranchId, int ProductId)>();
        var counted = new HashSet<int>();

        foreach (var product in rows.Values)
        {
            if (!product.Path.StartsWith(subtreePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (product.ParentId == nodeId)
            {
                AddDirect(product, linked: false);
            }
            else
            {
                AddToBranch(ReadId(product.Path, childOffset), product);
            }
        }

        // A product appears once per category, however many ways it is connected to it.
        foreach (var link in links)
        {
            if (!rows.TryGetValue(link.ProductId, out var product))
            {
                continue;
            }

            if (link.CategoryId == nodeId)
            {
                AddDirect(product, linked: true);
            }
            else if (link.CategoryPath.StartsWith(subtreePrefix, StringComparison.Ordinal))
            {
                AddToBranch(ReadId(link.CategoryPath, childOffset), product);
            }
        }

        return new CatalogCollectionIndex(direct.Values.ToList(), branches, counted.Count);

        decimal StockOf(CatalogProductRow product)
        {
            if (!stock.TryGetValue(product.Id, out var value))
            {
                value = variantCounts.GetValueOrDefault(product.Id) > 0
                    ? variantStock.GetValueOrDefault(product.Id)
                    : getStock(product.UniqueId);
                stock[product.Id] = value;
            }

            return value;
        }

        void AddDirect(CatalogProductRow product, bool linked)
        {
            if (direct.ContainsKey(product.Id))
            {
                return;
            }

            counted.Add(product.Id);
            direct[product.Id] = new CatalogProductEntry
            {
                Id = product.Id,
                Name = product.Name ?? string.Empty,
                SortOrder = product.SortOrder,
                CreateDate = product.CreateDate,
                UpdateDate = product.UpdateDate,
                Published = product.Published,
                VariantCount = variantCounts.GetValueOrDefault(product.Id),
                Stock = StockOf(product),
                Linked = linked,
            };
        }

        void AddToBranch(int childId, CatalogProductRow product)
        {
            if (childId == 0 || !placed.Add((childId, product.Id)))
            {
                return;
            }

            counted.Add(product.Id);
            if (!branches.TryGetValue(childId, out var branch))
            {
                branch = new CatalogBranchSummary();
                branches[childId] = branch;
            }

            branch.Total++;
            if (product.Published)
            {
                branch.Live++;
            }

            if (StockOf(product) <= 0)
            {
                branch.OutOfStock++;
            }
        }
    }

    public static string NormalizeStatus(string? status)
    {
        return (status ?? string.Empty).ToUpperInvariant() switch
        {
            "PUBLISHED" => "published",
            "UNPUBLISHED" => "unpublished",
            "OOS" => "oos",
            _ => "all",
        };
    }

    public static CatalogCollectionStatusCounts CountStatuses(IReadOnlyList<CatalogProductEntry> products)
    {
        var counts = new CatalogCollectionStatusCounts { All = products.Count };
        foreach (var product in products)
        {
            if (product.Published)
            {
                counts.Published++;
            }
            else
            {
                counts.Unpublished++;
            }

            if (product.Stock <= 0)
            {
                counts.OutOfStock++;
            }
        }

        return counts;
    }

    public static IReadOnlyList<CatalogProductEntry> FilterByStatus(IReadOnlyList<CatalogProductEntry> products, string status)
    {
        return status switch
        {
            "published" => products.Where(x => x.Published).ToList(),
            "unpublished" => products.Where(x => !x.Published).ToList(),
            "oos" => products.Where(x => x.Stock <= 0).ToList(),
            _ => products,
        };
    }

    public static List<CatalogProductEntry> Sort(IReadOnlyList<CatalogProductEntry> products, string? sort)
    {
        var byName = StringComparer.InvariantCultureIgnoreCase;

        return (sort ?? string.Empty).ToUpperInvariant() switch
        {
            "SORTORDERDESC" => products.OrderByDescending(x => x.SortOrder).ThenBy(x => x.Name, byName).ThenBy(x => x.Id).ToList(),
            "NAMEASC" => products.OrderBy(x => x.Name, byName).ThenBy(x => x.Id).ToList(),
            "NAMEDESC" => products.OrderByDescending(x => x.Name, byName).ThenBy(x => x.Id).ToList(),
            "CREATEDASC" => products.OrderBy(x => x.CreateDate).ThenBy(x => x.Id).ToList(),
            "CREATEDDESC" => products.OrderByDescending(x => x.CreateDate).ThenBy(x => x.Id).ToList(),
            "UPDATEDASC" => products.OrderBy(x => x.UpdateDate).ThenBy(x => x.Id).ToList(),
            "UPDATEDDESC" => products.OrderByDescending(x => x.UpdateDate).ThenBy(x => x.Id).ToList(),
            _ => products.OrderBy(x => x.SortOrder).ThenBy(x => x.Name, byName).ThenBy(x => x.Id).ToList(),
        };
    }

    /// <summary>
    /// Product totals including all subcategories, from direct counts per parent.
    /// </summary>
    public static IReadOnlyDictionary<int, int> GetRecursiveTotals(
        IEnumerable<(int Id, int ParentId)> categories,
        IReadOnlyDictionary<int, int> directCounts)
    {
        var children = new Dictionary<int, List<int>>();
        var ids = new List<int>();
        foreach (var (id, parentId) in categories)
        {
            ids.Add(id);
            if (!children.TryGetValue(parentId, out var list))
            {
                list = new List<int>();
                children[parentId] = list;
            }

            list.Add(id);
        }

        var totals = new Dictionary<int, int>(ids.Count);
        foreach (var id in ids)
        {
            Total(id);
        }

        return totals;

        int Total(int id)
        {
            if (totals.TryGetValue(id, out var known))
            {
                return known;
            }

            // Reserve the slot first so a malformed parent loop cannot recurse forever.
            totals[id] = 0;
            var total = directCounts.GetValueOrDefault(id);
            if (children.TryGetValue(id, out var list))
            {
                foreach (var child in list)
                {
                    total += Total(child);
                }
            }

            totals[id] = total;
            return total;
        }
    }

    private static int FindOwner(string path, HashSet<int> productIds)
    {
        // A variant's path ends ...,product,variantGroup,variant: walk back to the product.
        var end = path.LastIndexOf(',');
        while (end > 0)
        {
            var start = path.LastIndexOf(',', end - 1);
            if (int.TryParse(path.AsSpan(start + 1, end - start - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                && productIds.Contains(id))
            {
                return id;
            }

            end = start;
        }

        return 0;
    }

    private static int ReadId(string path, int offset)
    {
        if (offset >= path.Length)
        {
            return 0;
        }

        var end = path.IndexOf(',', offset);
        var segment = end < 0 ? path.AsSpan(offset) : path.AsSpan(offset, end - offset);
        return int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0;
    }
}
