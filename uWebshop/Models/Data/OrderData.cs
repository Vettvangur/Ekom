using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Persistence;
using uWebshop.Interfaces;

namespace uWebshop.Models.Data
{
    [TableName("uWebshopOrders")]
    [PrimaryKey("UniqueId", autoIncrement = false)]
    public class OrderData
    {
        public Guid UniqueId { get; set; }
        public int ReferenceId { get; set; }
        public string OrderInfo { get; set; }
        public string OrderNumber { get; set; }
        public string OrderStatus { get; set; }
        public string CustomerEmail { get; set; }
        public string CustomerName { get; set; }
        public int CustomerId { get; set; }
        public string CustomerUsername { get; set; }
        public string StoreAlias { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public DateTime PaidDate { get; set; }
    }
}
