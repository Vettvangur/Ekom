using Ekom.Models;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface ICategory : INodeEntityWithUrl, IPerStoreNodeEntity
    {
        int ParentCategoryId { get; set; }
        IEnumerable<IProduct> Products { get; }
        IEnumerable<IProduct> ProductsRecursive { get; }
        ICategory RootCategory { get; }
        IStore Store { get; }
        IEnumerable<ICategory> SubCategories { get; }
        IEnumerable<ICategory> SubCategoriesRecursive { get; }

        IEnumerable<ICategory> Ancestors();
    }
}
