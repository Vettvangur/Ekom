using System;
using System.Collections.Generic;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Base node entity interface.
    /// All base properties common to umbraco nodes
    /// </summary>
    public interface INodeEntity
    {
        /// <summary>
        /// 
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Gets the unique identifier.
        /// </summary>
        /// <value>
        /// The unique identifier.
        /// </value>
        int Id { get; }

        /// <summary>
        /// Gets the unique key identifier.
        /// </summary>
        /// <value>
        /// The unique key identifier.
        /// </value>
        Guid Key { get; }

        /// <summary>
        /// Gets the name or alias for the type. (NodeTypeAlias/ContentTypeAlias in Umbraco)
        /// </summary>
        /// <value>
        /// The type alias.
        /// </value>
        string ContentTypeAlias { get; }

        /// <summary>
        /// Gets the created date
        /// </summary>
        /// <value>
        /// The create date.
        /// </value>
        DateTime CreateDate { get; }

        /// <summary>
        /// Gets the update date
        /// </summary>
        /// <value>
        /// The update date.
        /// </value>
        DateTime UpdateDate { get; }

        /// <summary>
        /// SortOrder for the node
        /// </summary>
        /// <value>
        /// The sort order.
        /// </value>
        int SortOrder { get; }

        /// <summary>
        /// Level for the node
        /// </summary>
        /// <value>
        /// The level.
        /// </value>
        int Level { get; }

        /// <summary>
        /// Path for the node
        /// </summary>
        /// <value>
        /// The path.
        /// </value>
        string Path { get; }

        /// <summary>
        /// Get value from properties
        /// </summary>
        /// <value>
        /// The path.
        /// </value>
        string GetPropertyValue(string alias);
    }

    /// <summary>
    /// Node entity with URL/URLs
    /// </summary>
    public interface INodeEntityWithUrl : INodeEntity
    {
        /// <summary>
        /// Urls for the node
        /// </summary>
        /// <value>
        /// The urls.
        /// </value>
        IEnumerable<string> Urls { get; }

        /// <summary>
        /// The url for the node
        /// </summary>
        /// <value>
        /// The url.
        /// </value>
        string Url { get; }
    }
}
