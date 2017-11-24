using Ekom.Cache;
using Ekom.Interfaces;
using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// A group of variants sharing common properties and a group key
    /// </summary>
    public class VariantGroup : NodeEntity
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
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        /// <param name="cache"></param>
        public VariantGroup(Store store, IPerStoreCache<Variant> cache)
        {
            _variantCache = cache;
            _store = store;
        }

        /// <summary>
        /// Construct Variant Group from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public VariantGroup(SearchResult item, Store store) : base(item)
        {
            _store = store;
        }

        /// <summary>
        /// Construct Variant Group from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public VariantGroup(IContent node, Store store) : base(node)
        {
            _store = store;
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
