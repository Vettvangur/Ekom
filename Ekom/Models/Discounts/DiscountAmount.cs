using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Models.Discounts
{
    /// <summary>
    /// A discount applied to <see cref="OrderInfo"/> or <see cref="OrderLine"/>
    /// </summary>
    public class DiscountAmount
    {
        /// <summary>
        /// Fixed or percentage?
        /// </summary>
        public DiscountType Type { get; set; }

        /// <summary>
        /// 28.35 = 28.35% 
        /// </summary>
        public double Amount { get; }
    }
}
