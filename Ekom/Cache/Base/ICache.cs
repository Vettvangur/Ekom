using Ekom.Models;
using System;

namespace Ekom.Cache
{
    /// <summary>
    /// Caches implementing this interface offer addition and removal methods
    /// for usage by umbraco event listeners
    /// </summary>
    public interface ICache
    {
        /// <summary>
        /// Umbraco Node Alias name used in Examine search
        /// </summary>
        string NodeAlias { get; }

        /// <summary>
        /// Handles addition of nodes when umbraco events fire
        /// </summary>
        void AddReplace(UmbracoContent node);

        /// <summary>
        /// Handles removal of nodes when umbraco events fire
        /// </summary>
        void Remove(Guid id);

        /// <summary>
        /// Handles initial population of cache data
        /// </summary>
        void FillCache();
    }
}
