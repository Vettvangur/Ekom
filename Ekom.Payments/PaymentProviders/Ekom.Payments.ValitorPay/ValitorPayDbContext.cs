using Ekom.Payments.ValitorPay;
using LinqToDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.ValitorPay;

internal class ValitorPayDbContext : LinqToDB.Data.DataConnection
{
    public ValitorPayDbContext(string connectionString) : base(LinqToDB.ProviderName.SqlServer, connectionString) { }

    public ITable<VirtualCard> VirtualCards => this.GetTable<VirtualCard>();
}
