using Ekom.Models;
using LinqToDB;

namespace Ekom.Repositories
{
    public class DbContext : LinqToDB.Data.DataConnection
    {
        public DbContext(string connectionString) : base(LinqToDB.ProviderName.SqlServer, connectionString) { }

        public ITable<CouponData> CouponData => this.GetTable<CouponData>();
        public ITable<CustomerData> CustomerData => this.GetTable<CustomerData>();
        public ITable<DiscountStockData> DiscountStockData => this.GetTable<DiscountStockData>();
        public ITable<OrderActivityLog> OrderActivityLog => this.GetTable<OrderActivityLog>();
        public ITable<OrderData> OrderData => this.GetTable<OrderData>();
        public ITable<StockData> StockData => this.GetTable<StockData>();
    }
}
