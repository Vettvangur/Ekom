using Examine;
using Umbraco.Core.Models;
using uWebshop.Helpers;

namespace uWebshop.Models
{
    /// <summary>
    /// Base Per Store Umbraco node entity
    /// </summary>
    public abstract class PerStoreNodeEntity : NodeEntity
    {
        internal Store _store;

        /// <summary>
        /// 
        /// </summary>
        public override string Title => Properties.GetStoreProperty("title", _store.Alias);

        /// <summary>
        /// ctor
        /// </summary>
        public PerStoreNodeEntity(Store store)
        {
            _store = store;
        }

        /// <summary>
        /// Construct from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public PerStoreNodeEntity(SearchResult item, Store store) : base(item)
        {
            _store = store;
        }

        /// <summary>
        /// Construct from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public PerStoreNodeEntity(IContent node, Store store) : base(node)
        {
            _store = store;
        }
    }
}
