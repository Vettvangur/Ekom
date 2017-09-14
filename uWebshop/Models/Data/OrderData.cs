using System;
using System.ComponentModel.DataAnnotations;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;

namespace uWebshop.Models.Data
{
    [TableName("uWebshopOrders")]
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

        [Length(100)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string OrderStatus { get; set; }

        [Length(200)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string CustomerEmail { get; set; }

        [Length(200)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string CustomerName { get; set; }

        [NullSetting(NullSetting = NullSettings.Null)]
        public int CustomerId { get; set; }

        [Length(200)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string CustomerUsername { get; set; }

        [Length(50)]
        [NullSetting(NullSetting = NullSettings.Null)]
        public string StoreAlias { get; set; }

        [NullSetting(NullSetting = NullSettings.Null)]
        public DateTime CreateDate { get; set; }

        [NullSetting(NullSetting = NullSettings.Null)]
        public DateTime UpdateDate { get; set; }

        [NullSetting(NullSetting = NullSettings.Null)]
        public DateTime PaidDate { get; set; }
    }
}
