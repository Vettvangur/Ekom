using Ekom.Models;

namespace Ekom.Utilities
{
    public static class CategoryExtensions
    {
        public static bool IsCurrent(this ICategory category, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }
            
            return path.Split(',').Contains(category.Id.ToString());
        }
    }
}
