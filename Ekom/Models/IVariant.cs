using System;
using System.Collections.Generic;

namespace Ekom.Models
{
    /// <summary>
    /// A customization of a parent product, currently must belong to a <see cref="IVariantGroup"/>
    /// Price of variant is added to product base price to calculate total price.
    /// Has seperate stock from base product.
    /// </summary>
    public interface IVariant : IPerStoreNodeEntity
    {
        /// <summary>
        /// Gets the price.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        IPrice Price { get; }

        /// <summary>
        /// Gets the prices.
        /// </summary>
        /// <value>
        /// The price.
        /// </value>
        List<IPrice> Prices { get; }

        /// <summary>
        /// Gets the Vat.
        /// </summary>
        /// <value>
        /// The stock.
        /// </value>
        decimal Vat { get; }

        /// <summary>
        /// Gets the orignal price.
        /// </summary>
        /// <value>
        /// The original price.
        /// </value>
        IPrice OriginalPrice { get; }

        /// <summary>
        /// Parent product
        /// </summary>
        IProduct Product { get; }
        /// <summary>
        /// Parent product Id
        /// </summary>
        int ProductId { get; }
        /// <summary>
        /// Parent variantGroup Id
        /// </summary>
        int VariantGroupId { get; }

        /// <summary>
        /// Parent product Key
        /// </summary>
        Guid ProductKey { get; }
        /// <summary>
        /// Product Stock Keeping Unit.
        /// </summary>
        IProductDiscount ProductDiscount(string price);
        
        /// <summary>
        /// Get SKU
        /// </summary>
        string SKU { get; }

        /// <summary>
        /// Get Backorder status
        /// </summary>
        bool Backorder { get; }

        /// <summary>
        /// Gets the stock.
        /// </summary>
        /// <value>
        /// The stock.
        /// </value>
        int Stock { get; }

        /// <summary>
        /// 
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the availability of the variant.
        /// </summary>
        /// <value>
        /// The availability.
        /// </value>
        bool Available { get; }

        /// <summary>
        /// Variant group <see cref="IVariant"/> belongs to
        /// </summary>
        IVariantGroup VariantGroup { get; }

        /// <summary>
        /// All categories product belongs to, includes parent category.
        /// Does not include categories product is an indirect child of.
        /// </summary>
        List<ICategory> Categories();

        /// <summary>
        /// Variant images
        /// </summary>
        IEnumerable<Image> Images { get; }

        void InvalidateCache();
    }
}
