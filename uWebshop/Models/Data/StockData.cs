using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseAnnotations;
using uWebshop.Interfaces;

namespace uWebshop.Models.Data
{
    /// <summary>
    /// Current stock of a given item
    /// </summary>
    [TableName("uWebshopStock")]
    [PrimaryKey("UniqueId", autoIncrement = false)]
    public class StockData
    {
        [PrimaryKeyColumn(AutoIncrement = false)]
        public Guid UniqueId { get; set; }

        public int Stock { get; set; }

        public Guid NodeId { get; set; }

        public string StoreAlias { get; set; }

        public DateTime CreateDate { get; set; }

        public DateTime UpdateDate { get; set; }

        public StockData()
        {

        }
    }
}
