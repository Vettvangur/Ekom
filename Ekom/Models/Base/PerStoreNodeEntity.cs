using Examine;
using Umbraco.Core.Models;
using Ekom.Helpers;

namespace Ekom.Models
{
    /// <summary>
    /// Base Per Store Umbraco node entity
    /// </summary>
    public abstract class PerStoreNodeEntity : NodeEntity, IPerStoreNodeEntity
    {
        internal Store store;

        /// <summary>
        /// 
        /// </summary>
        public override string Title => Properties.GetStoreProperty("title", store.Alias);

        /// <summary>
        /// ctor
        /// </summary>
        public PerStoreNodeEntity(Store store)
        {
            this.store = store;
        }

        /// <summary>
        /// Construct from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public PerStoreNodeEntity(SearchResult item, Store store) : base(item)
        {
            this.store = store;
        }

        /// <summary>
        /// Construct from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public PerStoreNodeEntity(IContent node, Store store) : base(node)
        {
            this.store = store;
        }
    }
}
