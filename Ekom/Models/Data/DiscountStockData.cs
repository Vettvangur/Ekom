using NPoco;
using System;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Ekom.Models.Data
{
    /// <summary>
    /// Current stock of a given item
    /// </summary>
    [TableName(Configuration.DiscountStockTableName)]
    [PrimaryKey("UniqueId", AutoIncrement = false)]
    public class DiscountStockData
    {
        /// <summary>
        /// Composed from discount <see cref="Guid"/> and coupon name if present.
        /// <para />
        /// $"{uniqueId}_{coupon}" or:
        /// d6da9d30-9246-4856-b66b-5411086b84d9_supercoupon
        /// <para />
        /// In other cases this is simply a Guid
        /// </summary>
        [Length(137)]
        [PrimaryKeyColumn(AutoIncrement = false)]
        public string UniqueId { get; set; }

        /// <summary>
        /// Unit count
        /// </summary>
        public int Stock { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTime CreateDate { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public DateTime UpdateDate { get; set; }
    }
}
