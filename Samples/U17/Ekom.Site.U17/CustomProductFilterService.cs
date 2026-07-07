using Ekom.Models;
using Ekom.Services;

namespace Ekom.Site.U17;

public class CustomProductFilterService : ProductFilterService
{
    public override IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null)
        => base.ApplyFilters(products, query, category);

    public override Task<IEnumerable<IProduct>> ApplyFiltersAsync(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        return base.ApplyFiltersAsync(products, query, category, ct);
    }
}
