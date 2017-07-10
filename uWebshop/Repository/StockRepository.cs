using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using uWebshop.Models.Data;

namespace uWebshop.Repository
{
    public class StockRepository
    {
        public StockData GetStockById(Guid uniqueId)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.FirstOrDefault<StockData>("SELECT * FROM uWebshopStock WHERE UniqueId = @0", uniqueId);
            }
        }
        public IEnumerable<StockData> GetStockByNodeId(Guid uniqueId)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<StockData>("SELECT * FROM uWebshopStock WHERE NodeId = @0", uniqueId);
            }
        }

        public IEnumerable<StockData> GetStockByStore(string storeAlias)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<StockData>("SELECT * FROM uWebshopStock WHERE StoreAlias = @0", storeAlias);
            }
        }

        public IEnumerable<StockData> GetStockByNodeIdAndStore(Guid uniqueId, string storeAlias)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<StockData>("SELECT * FROM uWebshopStock WHERE StoreAlias = @0 AND NodeId = @1", storeAlias, uniqueId);
            }
        }

        public IEnumerable<StockData> GetAllStock()
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<StockData>("SELECT * FROM uWebshopStock");
            }
        }
    }
}
