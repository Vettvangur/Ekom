using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;

namespace Ekom.Interfaces
{
    /// <summary>
    /// Groups multiple <see cref="IVariant"/>'s together with a group key, 
    /// common properties and a shared parent <see cref="IProduct"/>
    /// </summary>
    public interface IVariantGroup : INodeEntity
    {
        /// <summary>
        /// Variant group Images
        /// </summary>
        IEnumerable<IPublishedContent> Images { get; }
        /// <summary>
        /// Parent <see cref="IProduct"/> of Variant
        /// </summary>
        IProduct Product { get; }
        /// <summary>
        /// Id of Parent <see cref="IProduct"/> of Variant
        /// </summary>
        int ProductId { get; }
        /// <summary>
        /// Key of Parent <see cref="IProduct"/> of Variant
        /// </summary>
        Guid ProductKey { get; }
        /// <summary>
        /// Get all variants in this group
        /// </summary>
        IEnumerable<IVariant> Variants { get; }
    }
}
