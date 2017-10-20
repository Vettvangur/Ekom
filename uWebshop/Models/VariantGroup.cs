using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using uWebshop.Cache;

namespace uWebshop.Models
{
    /// <summary>
    /// A group of variants sharing common properties and a group key
    /// </summary>
    public class VariantGroup
    {
        Store _store { get; set; }
        IPerStoreCache<Variant> _variantCache;

        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Guid Key { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public Guid ProductKey { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int SortOrder { get; set; }
        /// <summary>
        /// Get all variants in this group
        /// </summary>
        [JsonIgnore]
        public IEnumerable<Variant> Variants
        {
            get
            {
                return _variantCache.Cache[_store.Alias]
                                   .Where(x => x.Value.VariantGroupKey == Key)
                                   .Select(x => x.Value);
            }
        }

        /// <summary>
        /// Used by uWebshop extensions
        /// </summary>
        /// <param name="store"></param>
        /// <param name="cache"></param>
        public VariantGroup(Store store, IPerStoreCache<Variant> cache)
        {
            _variantCache = cache;
            _store = store;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
