using Ekom.Models;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Interfaces
{
    public interface IActivityLogRepository
    {
        void Insert(Guid Key, string Log, string UserName);
        IEnumerable<OrderActivityLog> GetLatestActivityLogsOrdersByUser(string userName);
        IEnumerable<OrderActivityLog> GetLatestActivityLogsOrders();
        IEnumerable<OrderActivityLog> GetLogs(string OrderNumber);
        IEnumerable<OrderActivityLog> GetLogs(Guid uniqueId);

    }
}
