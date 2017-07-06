using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Interfaces;

namespace uWebshop.Models
{
    public class OrderInfo : IOrderInfo
    {
        public Guid UniqueId { get; set; }
        public int ReferenceId { get; set; }
        public string OrderNumber { get; set; }
        public List<OrderLine> OrderLines = new List<OrderLine>();
        public int Quantity {
            get
            {
                return OrderLines != null && OrderLines.Any() ? OrderLines.Sum(x => x.Quantity) : 0;
            }
        }
        public StoreInfo StoreInfo { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
        public DateTime PaidDate { get; set; }
    }
}
