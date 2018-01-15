using Ekom.Models;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    public interface ICategory : INodeEntityWithUrl
    {
        int ParentCategoryId { get; set; }
        IEnumerable<IProduct> Products { get; }
        IEnumerable<IProduct> ProductsRecursive { get; }
        ICategory RootCategory { get; }
        Store Store { get; }
        IEnumerable<ICategory> SubCategories { get; }
        IEnumerable<ICategory> SubCategoriesRecursive { get; }

        IEnumerable<ICategory> Ancestors();
        string GetPropertyValue(string alias);
    }
}
