using Ekom.Models;
using LinqToDB;
using LinqToDB.Data;

namespace Ekom.Services;

internal partial class DatabaseService
{
    internal virtual void EnsureOrderStateColumns()
    {
        using var db = _databaseFactory.GetDatabase();
        db.CreateTable<OrderData>(tableOptions: TableOptions.CreateIfNotExists);
        var query = _databaseFactory.IsSqlServer
            ? "SELECT name FROM sys.columns WHERE object_id = OBJECT_ID('EkomOrders')"
            : "SELECT name FROM pragma_table_info('EkomOrders')";
        var columns = db.Query<string>(query).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var text = _databaseFactory.IsSqlServer ? "nvarchar(64)" : "TEXT";
        var definitions = new Dictionary<string, string>
        {
            [nameof(OrderData.OrderStateCol)] = text + " NULL",
            [nameof(OrderData.PaymentStateCol)] = text + " NULL",
            [nameof(OrderData.FulfillmentStateCol)] = text + " NULL",
            [nameof(OrderData.OriginalOrderStatusCol)] = text + " NULL",
            [nameof(OrderData.FulfilledQuantitiesJson)] = (_databaseFactory.IsSqlServer ? "nvarchar(max)" : "TEXT") + " NULL",
            [nameof(OrderData.StatusMappingVersion)] = "int NOT NULL DEFAULT 0",
            [nameof(OrderData.StatusRevision)] = "int NOT NULL DEFAULT 0",
            [nameof(OrderData.AutoCloseSuppressed)] = (_databaseFactory.IsSqlServer ? "bit" : "INTEGER") + " NOT NULL DEFAULT 0",
        };
        foreach (var (name, definition) in definitions)
        {
            if (!columns.Contains(name)) db.Execute($"ALTER TABLE EkomOrders ADD [{name}] {definition}");
        }
    }
}
