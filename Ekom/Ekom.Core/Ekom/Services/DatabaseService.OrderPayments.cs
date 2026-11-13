using Ekom.Models.Payments;
using Ekom.Repositories;
using LinqToDB;
using LinqToDB.Data;

namespace Ekom.Services;

internal partial class DatabaseService
{
    /// <summary>Creates the order payment tables and their indexes. Safe to run on every startup.</summary>
    internal virtual void EnsureOrderPaymentTables()
    {
        using var db = _databaseFactory.GetDatabase();
        db.CreateTable<OrderPayment>(tableOptions: TableOptions.CreateIfNotExists);
        db.CreateTable<OrderPaymentTransaction>(tableOptions: TableOptions.CreateIfNotExists);

        EnsurePaymentIndex(db, "EkomOrderPayments", "IX_EkomOrderPayments_PaymentId", true, nameof(OrderPayment.PaymentId));
        EnsurePaymentIndex(db, "EkomOrderPayments", "IX_EkomOrderPayments_OrderUniqueId", false, nameof(OrderPayment.OrderUniqueId));
        EnsurePaymentIndex(db, "EkomOrderPayments", "IX_EkomOrderPayments_TransactionId", false, nameof(OrderPayment.TransactionId));
        EnsurePaymentIndex(db, "EkomOrderPayments", "IX_EkomOrderPayments_ProviderReference", false, nameof(OrderPayment.ProviderReference));
        EnsurePaymentIndex(db, "EkomOrderPaymentTransactions", "IX_EkomOrderPaymentTransactions_IdempotencyKey", true, nameof(OrderPaymentTransaction.IdempotencyKey));
        EnsurePaymentIndex(db, "EkomOrderPaymentTransactions", "IX_EkomOrderPaymentTransactions_OrderUniqueId", false, nameof(OrderPaymentTransaction.OrderUniqueId));
        EnsurePaymentIndex(db, "EkomOrderPaymentTransactions", "IX_EkomOrderPaymentTransactions_PaymentId", false, nameof(OrderPaymentTransaction.PaymentId));
        EnsurePaymentIndex(db, "EkomOrderPaymentTransactions", "IX_EkomOrderPaymentTransactions_TransactionId", false, nameof(OrderPaymentTransaction.TransactionId));
    }

    private void EnsurePaymentIndex(DbContext db, string table, string name, bool unique, string column)
    {
        var kind = unique ? "UNIQUE INDEX" : "INDEX";
        if (_databaseFactory.IsSqlServer)
        {
            db.Execute($@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{name}' AND object_id = OBJECT_ID('{table}'))
    CREATE {kind} {name} ON {table} ([{column}]);");
        }
        else
        {
            db.Execute($"CREATE {kind} IF NOT EXISTS {name} ON {table} ([{column}])");
        }
    }
}
