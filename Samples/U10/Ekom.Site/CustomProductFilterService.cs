using Ekom.Models;
using Ekom.Services;

namespace Ekom.Site;

public class CustomProductFilterService : ProductFilterService
{
    public CustomProductFilterService()
        : base()
    {
    }

    public override IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null)
    {

        //var isAdmin = true;

        //if (isAdmin)
        //{
        //    return products.Where(x => x.SKU != "mini-sketchbooks");
        //}

        // Optionally call the base method if you want the default filtering logic
        return base.ApplyFilters(products);
    }

    public override Task<IEnumerable<IProduct>> ApplyFiltersAsync(IEnumerable<IProduct> products, ProductQuery? query = null, ICategory? category = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        //var isAdmin = true;

        //if (isAdmin)
        //{
        //    return Task.FromResult(products.Where(x => x.SKU != "mini-sketchbooks"));
        //}

        // Optionally call the base method if you want the default filtering logic
        return base.ApplyFiltersAsync(products, query, category, ct);
    }
}
