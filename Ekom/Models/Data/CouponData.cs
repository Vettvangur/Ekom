using NPoco;
using System;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Ekom.Models.Data
{
    /// <summary>
    /// Current stock of a given item
    /// </summary>
    [TableName("EkomCoupon")]
    public class CouponData
    {
        /// <summary>
        /// Identity column in the database primary key
        /// </summary>
        [PrimaryKeyColumn(AutoIncrement = true, Clustered = false)]
        public int Id { get; set; }
        /// <summary>
        /// String that is distrubuted as the coupon
        /// </summary>
        public string CouponCode { get; set; }
        /// <summary>
        /// How many times the coupon can be used
        /// </summary>
        public int NumberAvailable { get; set; }
        /// <summary>
        /// Unique identifier for the Key that can be exposed
        /// </summary>
        public Guid CouponKey { get; set; }
        /// <summary>
        /// The node Key for the discount 
        /// </summary>
        public Guid DiscountId { get; set; }
    }
}
