using Ekom.Helpers;
using System;
using System.ComponentModel.DataAnnotations;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace Ekom.Models.Data
{
    /// <summary>
    /// SQL Representation of <see cref="OrderInfo"/>
    /// </summary>
    [TableName("EkomOrders")]
    [PrimaryKey("UniqueId", autoIncrement = false)]
    public class OrderData
    {
        [PrimaryKeyColumn(AutoIncrement = false)]
        public Guid UniqueId { get; set; }
        public int ReferenceId { get; set; }

        [StringLength(int.MaxValue, MinimumLength = 3)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string OrderInfo { get; set; }

        [Length(100)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string OrderNumber { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public OrderStatus OrderStatus { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Length(200)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string CustomerEmail { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Length(200)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string CustomerName { get; set; }

        [NullSetting(NullSetting = NullSettings.Null)]
        public int CustomerId { get; set; }

        [Length(200)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string CustomerUsername { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [Length(50)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string StoreAlias { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [NullSetting(NullSetting = NullSettings.Null)]
        public DateTime CreateDate { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [NullSetting(NullSetting = NullSettings.Null)]
        public DateTime UpdateDate { get; set; }

        /// <summary>
        /// <see cref="DateTime"/> payment was verified.
        /// </summary>
        [NullSetting(NullSetting = NullSettings.Null)]
        public DateTime PaidDate { get; set; }
    }
}
