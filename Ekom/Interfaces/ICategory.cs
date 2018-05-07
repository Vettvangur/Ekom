using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Categories are groupings of products, categories can also be nested, f.x.
    /// Women->Winter->Shirts
    /// </summary>
    public interface ICategory : INodeEntityWithUrl, IPerStoreNodeEntity
    {
        /// <summary>
        /// Parent umbraco node
        /// </summary>
        int ParentId { get; set; }
        /// <summary>
        /// All direct child products of category. (No descendants)
        /// </summary>
        IEnumerable<IProduct> Products { get; }
        /// <summary>
        /// All descendant products of category, this includes child products of sub-categories
        /// </summary>
        IEnumerable<IProduct> ProductsRecursive { get; }
        /// <summary>
        /// Our eldest ancestor category
        /// </summary>
        ICategory RootCategory { get; }
        /// <summary>
        /// All direct child categories
        /// </summary>
        IEnumerable<ICategory> SubCategories { get; }
        /// <summary>
        /// All descendant categories, includes grandchild categories
        /// </summary>
        IEnumerable<ICategory> SubCategoriesRecursive { get; }

        /// <summary>
        /// All parent categories, grandparent categories and so on.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ICategory> Ancestors();
    }
}
