using System.Collections.Generic;

namespace Ekom.Models
{
    /// <summary>
    /// Categories are groupings of products, categories can also be nested, f.x.
    /// Women->Winter->Shirts
    /// </summary>
    public interface ICategory : INodeEntityWithUrl, IPerStoreNodeEntity
    {
        /// <summary>
        /// All direct child products of category. (No descendants)
        /// </summary>
        ProductResponse Products(ProductQuery query = null);

        /// <summary>
        /// All descendant products of category, this includes child products of sub-categories
        /// </summary>
        ProductResponse ProductsRecursive(ProductQuery query = null);

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

        IEnumerable<MetafieldGrouped> Filters(bool filterable = true);
    }
}
