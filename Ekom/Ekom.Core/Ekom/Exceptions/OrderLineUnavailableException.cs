namespace Ekom.Exceptions
{
    /// <summary>
    /// An order line holds a product or variant that no longer exists in the store,
    /// so checkout cannot continue until the line is removed.
    /// </summary>
    public class OrderLineUnavailableException : StockException
    {
        /// <summary>The order line to remove.</summary>
        public Guid OrderLineKey { get; }

        /// <summary>Key of the missing product or variant.</summary>
        public Guid ItemKey { get; }

        /// <summary>True when the missing item is a variant, false when it is the product.</summary>
        public bool IsVariant { get; }

        /// <summary>SKU stored on the order line for the missing item.</summary>
        public string? Sku { get; }

        /// <summary>Product title stored on the order line.</summary>
        public string? Title { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderLineUnavailableException"/> class.
        /// </summary>
        public OrderLineUnavailableException(Guid orderLineKey, Guid itemKey, bool isVariant, string? sku, string? title, string? storeAlias)
            : base($"{(isVariant ? "Variant" : "Product")} {itemKey} (SKU: {Display(sku)}, title: {Display(title)}) "
                + $"on order line {orderLineKey} is missing from store {Display(storeAlias)}.")
        {
            OrderLineKey = orderLineKey;
            ItemKey = itemKey;
            IsVariant = isVariant;
            Sku = sku;
            Title = title;
        }

        private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }
}
