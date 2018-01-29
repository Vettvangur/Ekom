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
        public DiscountType Type { get; internal set; }

        /// <summary>
        /// Umbraco input: 28.5 <para></para>
        /// Stored value: 0.285<para></para>
        /// Effective value: 28.5%<para></para>
        /// </summary>
        public decimal Amount { get; internal set; }
    }
}
