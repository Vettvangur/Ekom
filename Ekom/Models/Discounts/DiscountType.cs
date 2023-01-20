namespace Ekom.Models
{
    /// <summary>
    /// Fixed or percentage?
    /// </summary>
    public enum DiscountType
    {
        /// <summary>
        /// Fixed amount to deduct from <see cref="OrderInfo"/>/<see cref="OrderLine"/>
        /// </summary>
        Fixed,
        /// <summary>
        /// Deduct a percentage based amount from <see cref="OrderInfo"/>/<see cref="OrderLine"/>
        /// </summary>
        Percentage
    };
}
