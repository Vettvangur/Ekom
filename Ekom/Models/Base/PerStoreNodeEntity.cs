using Examine;
using Umbraco.Core.Models;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Utilities;
using Newtonsoft.Json;

namespace Ekom.Models
{
    /// <summary>
    /// Base Per Store Umbraco node entity
    /// </summary>
    public abstract class PerStoreNodeEntity : NodeEntity, IPerStoreNodeEntity
    {
        /// <summary>
        /// Store this node entity belongs to
        /// </summary>
        [JsonIgnore]
        public IStore Store { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public override string Title => Properties.GetPropertyValue("title", Store.Alias);

        /// <summary>
        /// ctor
        /// </summary>
        public PerStoreNodeEntity(IStore store)
        {
            Store = store;
        }

        /// <summary>
        /// Construct from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public PerStoreNodeEntity(SearchResult item, IStore store) : base(item)
        {
            Store = store;
        }

        /// <summary>
        /// Construct from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public PerStoreNodeEntity(IContent node, IStore store) : base(node)
        {
            Store = store;
        }
    }
}
