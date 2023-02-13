using LinqToDB;

namespace Ekom.Payments;

public class DbContext : LinqToDB.Data.DataConnection
{
    public DbContext(string connectionString) : base(LinqToDB.ProviderName.SqlServer, connectionString) { }

    public ITable<OrderStatus> OrderStatus => this.GetTable<OrderStatus>();
    public ITable<PaymentData> PaymentData => this.GetTable<PaymentData>();
}
