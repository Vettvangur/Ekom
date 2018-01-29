namespace Ekom.Interfaces
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
        /// Value with vat and discount if applicable
        /// </summary>
        bool IsDiscounted { get; }
        /// <summary>
        /// 
        /// </summary>
        string ToCurrencyString { get; }
    }

    /// <summary>
    /// Price of item including all data to fully calculate 
    /// before and after VAT/Discount.
    /// </summary>
    public interface IPrice : IVatPrice
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
        /// Value with discount and VAT
        /// </summary>
        decimal Value { get; }

        /// <summary>
        /// Gets the discount.
        /// </summary>
        /// <value>
        /// The discount.
        /// </value>
        IDiscount Discount { get; }
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

        // Var IPrice return type, virkar ekki gagnlegt
        /// <summary>
        /// Gets the vat.
        /// </summary>
        /// <value>
        /// The vat.
        /// </value>
        decimal Vat { get; }
    }
}
