using System;

namespace Ekom.Models
{
    /// <summary>
    /// An object that contains the calculated price given the provided parameters
    /// Also offers a way of printing the value using the provided culture.
    /// </summary>
    public interface ICalculatedPrice
    {
        /// <summary>
        /// Value with vat and discount if applicable
        /// </summary>
        decimal Value { get; }
        /// <summary>
        /// 
        /// </summary>
        string CurrencyString { get; }
    }

    /// <summary>
    /// Price of item including all data to fully calculate 
    /// before and after VAT/Discount.
    /// </summary>
    public interface IPrice : IVatPrice, ICloneable
    {
        /// <summary>
        /// Original Price
        /// </summary>
        decimal OriginalValue { get; }
        /// <summary>
        /// Gets the original price before the discount.
        /// Same as OriginalValue
        /// </summary>
        /// <value>
        /// The original price before the discount.
        /// Same as OriginalValue
        /// </value>
        ICalculatedPrice BeforeDiscount { get; }
        /// <summary>
        /// Price after discount with VAT left as-is
        /// </summary>
        ICalculatedPrice AfterDiscount { get; }
        /// <summary>
        /// VAT included or to be included in price
        /// </summary>
        ICalculatedPrice Vat { get; }
        /// <summary>
        /// Total monetary value of discount in price
        /// </summary>
        ICalculatedPrice DiscountAmount { get; }

        /// <summary>
        /// Value with discount and VAT
        /// </summary>
        decimal Value { get; }

        /// <summary>
        /// Gets the discount.
        /// </summary>
        /// <value>
        /// The discount.
        /// </value>
        OrderedDiscount Discount { get; }
        /// <summary>
        /// 
        /// </summary>
        CurrencyModel Currency { get; }
    }

    /// <summary>
    /// <see cref="IPrice"/> sub interface.
    /// Currently serves no obvious purpose..
    /// </summary>
    public interface IVatPrice
    {
        /// <summary>
        /// Gets the price with vat.
        /// </summary>
        /// <value>
        /// The price with vat.
        /// </value>
        ICalculatedPrice WithVat { get; }

        /// <summary>
        /// Gets the price without vat.
        /// </summary>
        /// <value>
        /// The price without vat.
        /// </value>
        ICalculatedPrice WithoutVat { get; }
    }
}
