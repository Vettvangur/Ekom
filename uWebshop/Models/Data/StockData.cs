using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Persistence;
using uWebshop.Interfaces;

namespace uWebshop.Models.Data
{
    [TableName("uWebshopStock")]
    [PrimaryKey("UniqueId", autoIncrement = false)]
    public class StockData
    {
        public Guid UniqueId { get; set; }
        public int Stock { get; set; }
        public Guid NodeId { get; set; }
        public string StoreAlias { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }
}
