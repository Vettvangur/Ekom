using LinqToDB;
using LinqToDB.Data;

namespace Ekom.Payments.AspNetCore;

class EnsureTablesExist
{
    readonly IDatabaseFactory _databaseFactory;

    public EnsureTablesExist(IDatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public void Create()
    {
        using var db = _databaseFactory.GetDatabase();
        
        var sp = db.DataProvider.GetSchemaProvider();
        var dbSchema = sp.GetSchema(db);

        if (!dbSchema.Tables.Any(t => t.TableName == "EkomPaymentOrders"))
        {
            db.CreateTable<OrderStatus>();
            db.Execute($"ALTER TABLE EkomPaymentOrders ALTER COLUMN EkomPaymentSettingsData NVARCHAR(MAX)");
            db.Execute(@"ALTER TABLE EkomPaymentOrders DROP COLUMN PaymentProviderName");
            db.Execute(@"ALTER TABLE EkomPaymentOrders ADD
                PaymentProviderName  AS json_value([EkomPaymentSettingsData],'$.PaymentProviderName')");
            db.Execute(@"ALTER TABLE EkomPaymentOrders DROP COLUMN PaymentProviderKey");
            db.Execute(@"ALTER TABLE EkomPaymentOrders ADD
                PaymentProviderKey  AS json_value([EkomPaymentSettingsData],'$.PaymentProviderKey')");

            db.Execute($"ALTER TABLE dbo.EkomPaymentOrders DROP CONSTRAINT [PK_EkomPaymentOrders]");
            db.Execute($"ALTER TABLE EkomPaymentOrders ADD CONSTRAINT [PK_EkomPaymentOrders] PRIMARY KEY NONCLUSTERED ([ReferenceId] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]");
            db.Execute($"CREATE UNIQUE NONCLUSTERED INDEX [IX_EkomPaymentOrders_UniqueId] ON EkomPaymentOrders ( [UniqueId] ASC )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]");
        }

        if (!dbSchema.Tables.Any(t => t.TableName == "EkomPayments"))
        {
            db.CreateTable<PaymentData>();
            db.Execute($"ALTER TABLE EkomPayments ALTER COLUMN CustomData NVARCHAR(MAX)");
        }
    }
}
