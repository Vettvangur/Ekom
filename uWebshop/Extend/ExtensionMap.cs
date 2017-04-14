using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Cache;

namespace uWebshop.Extend
{
    /// <summary>
    /// Extend the uwebshop solution
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Offers ways to extend caches by offering up <see cref="ICacheExtensions"/> to replace
        /// built in methods such as FillCache
        /// </summary>
        public static Dictionary<Type, ICacheExtensions> CacheExtensionMap =
                  new Dictionary<Type, ICacheExtensions>();
    }
}
