namespace Ekom.Models
{
    public class StockError
    {
        /// <summary>
        /// Product / Variant
        /// </summary>
        public bool IsVariant { get; set; }

        public Guid OrderLineKey { get; set; }

        public Exception Exception { get; set; }
    }
}
