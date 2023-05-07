using Ekom.Payments.ValitorPay;
using LinqToDB;
using LinqToDB.Common;
using LinqToDB.Data;
using Microsoft.Extensions.Configuration;
using Vettvangur.ValitorPay;

namespace Ekom.Payments.ValitorPay;

class EnsureValitorPayTablesExist
{
    readonly string _connectionString;

    public EnsureValitorPayTablesExist(IConfiguration configuration)
    {
        var connectionStringName = "umbracoDbDSN";
        _connectionString = configuration.GetConnectionString(connectionStringName);
    }

    public void Create()
    {
        using var db = new ValitorPayDbContext(_connectionString);
        
        var sp = db.DataProvider.GetSchemaProvider();
        var dbSchema = sp.GetSchema(db);

        if (!dbSchema.Tables.Any(t => t.TableName == "EkomValitorPayVirtualCards"))
        {
            db.CreateTable<VirtualCard>();
        }
    }
}
