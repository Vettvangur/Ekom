using Ekom.Models;

namespace Ekom.Services;

public interface IProductFilterService
{
    IEnumerable<IProduct> ApplyFilters(IEnumerable<IProduct> products);
}
