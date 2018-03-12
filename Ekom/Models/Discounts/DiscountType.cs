using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models.Discounts
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
