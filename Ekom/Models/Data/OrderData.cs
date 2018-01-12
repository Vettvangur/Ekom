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
        /// <summary>
        /// 
        /// </summary>
        [PrimaryKeyColumn(AutoIncrement = false)]
        public Guid UniqueId { get; set; }

        public int ReferenceId { get; set; }

        /// <summary>
        /// <see cref="Models.OrderInfo"/> Json
        /// </summary>
        [StringLength(int.MaxValue, MinimumLength = 3)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string OrderInfo { get; set; }

        [Length(100)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string OrderNumber { get; set; }

        /// <summary>
        /// The database representation of the enum.
        /// This is necessary for the creation of the column.
        /// </summary>
        public int OrderStatusCol { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [ResultColumn]
        public OrderStatus OrderStatus
        {
            get { return (OrderStatus)OrderStatusCol; }
            set { OrderStatusCol = (int)value; }
        }

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

        /// <summary>
        /// 
        /// </summary>
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
        /// Last update date
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
