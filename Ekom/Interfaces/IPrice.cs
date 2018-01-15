using Ekom.Models.Discounts;

namespace Ekom.Interfaces
{
    /// <summary>
    /// 
    /// </summary>
    public interface IPrice
    {
        decimal Value { get; }
        string ToCurrencyString { get; }
    }
    public interface IVatPrice
    {
        /// <summary>
        /// Gets the price with vat.
        /// </summary>
        /// <value>
        /// The price with vat.
        /// </value>
        IPrice WithVat { get; }

        /// <summary>
        /// Gets the price without vat.
        /// </summary>
        /// <value>
        /// The price without vat.
        /// </value>
        IPrice WithoutVat { get; }

        /// <summary>
        /// Gets the vat.
        /// </summary>
        /// <value>
        /// The vat.
        /// </value>
        IPrice Vat { get; }
    }

    public interface IDiscountedPrice : IVatPrice
    {
        /// <summary>
        /// Original Price
        /// </summary>
        decimal Value { get; }
        /// <summary>
        /// Gets the original price before the discount.
        /// </summary>
        /// <value>
        /// The original price before the discount.
        /// </value>
        IVatPrice BeforeDiscount { get; }

        /// <summary>
        /// Gets the discount.
        /// </summary>
        /// <value>
        /// The discount.
        /// </value>
        IDiscount Discount { get; }
    }


}
