using System;
using System.Collections.Generic;

namespace Ekom.Models
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
        IEnumerable<Image> Images { get; }
        /// <summary>
        /// Get the Primary variant price, if no variants then fallback to product price
        /// </summary>
        IPrice Price { get; }
        /// <summary>
        /// Parent <see cref="IProduct"/> of Variant
        /// </summary>
        IProduct Product { get; }
        /// <summary>
        /// Id of Parent <see cref="IProduct"/> of Variant
        /// </summary>

        /// <summary>
        /// Gets the availability of the variant group.
        /// </summary>
        /// <value>
        /// The availability.
        /// </value>
        bool Available { get; }

        int ProductId { get; }
        /// <summary>
        /// Key of Parent <see cref="IProduct"/> of Variant
        /// </summary>
        Guid ProductKey { get; }
        /// <summary>
        /// Get all variants in this group
        /// </summary>
        IEnumerable<IVariant> Variants { get; }

        /// <summary>
        /// Select the Primary variant.
        /// First Variant in the group that is available, if none are available, return the first variant.
        /// </summary>
        IVariant PrimaryVariant { get; }
    }
}
