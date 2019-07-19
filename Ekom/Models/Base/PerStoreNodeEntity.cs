using Ekom.Interfaces;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core.Models;

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
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
        public IStore Store { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public override string Title => Properties.GetPropertyValue("title", Store.Alias);

        /// <summary>
        /// Get value in properties by store
        /// </summary>
        /// <param name="alias"></param>
        /// <param name="storeAlias"></param>
        public virtual string GetPropertyValue(string alias, string storeAlias)
        {
            return Properties.GetPropertyValue(alias, storeAlias);
        }

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
        public PerStoreNodeEntity(ISearchResult item, IStore store) : base(item)
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
