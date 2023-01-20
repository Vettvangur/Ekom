using System;
using System.Collections.Generic;

namespace Ekom.Models
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
        /// Gets the parent ID
        /// </summary>
        /// <value>
        /// The parent ID
        /// </value>
        int ParentId { get; }
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
        /// Varies by culture
        /// </summary>
        /// <value>
        /// Boolean if node varies by culture
        /// </value>
        bool VariesByCulture { get; } 
        

        /// <summary>
        /// Path for the node
        /// </summary>
        /// <value>
        /// The path.
        /// </value>
        string Path { get; }

        /// <summary>
        /// Get value in properties by store or languge
        /// </summary>
        /// <param name="propAlias"></param>
        /// <param name="alias"></param>
        string GetValue(string propAlias, string alias = null);

        /// <summary>
        /// Umbraco node properties
        /// </summary>
        IReadOnlyDictionary<string, string> Properties { get; }
    }

    /// <summary>
    /// Node entity with URL/URLs
    /// </summary>
    public interface INodeEntityWithUrl : INodeEntity
    {
        /// <summary>
        /// All entity urls, computed from stores and possibly categories.
        /// </summary>
        IEnumerable<string> Urls { get; }

        /// <summary>
        /// Product url in relation to current request.
        /// This is a getter mostly for serialization purposes
        /// methods are ofc skipped by JSON.NET
        /// </summary>
        string Url { get; }

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        string Slug { get; }
    }
}
