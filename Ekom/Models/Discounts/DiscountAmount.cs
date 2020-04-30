using Newtonsoft.Json;

namespace Ekom.Models.Discounts
{
    /// <summary>
    /// A discount applied to <see cref="OrderInfo"/> or <see cref="OrderLine"/>
    /// </summary>
    public class DiscountAmount
    {
        public DiscountAmount()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        [JsonConstructor]
        public DiscountAmount(DiscountType type, decimal amount)
        {
            Type = type;
            Amount = amount;
        }

        /// <summary>
        /// Fixed or percentage?
        /// </summary>
        public DiscountType Type { get; internal set; }

        /// <summary>
        /// Umbraco input: 28.5 <para></para>
        /// Stored value: 0.285<para></para>
        /// Effective value: 28.5%<para></para>
        /// </summary>
        public decimal Amount { get; internal set; }
    }
}
