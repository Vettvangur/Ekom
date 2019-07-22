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
        Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsOrdersAsync();
        Task<IEnumerable<OrderActivityLog>> GetLatestActivityLogsOrdersByUserAsync(string userName);
        Task<IEnumerable<OrderActivityLog>> GetLogsAsync(Guid uniqueId);
        Task<IEnumerable<OrderActivityLog>> GetLogsAsync(string OrderNumber);
        Task InsertAsync(Guid Key, string Log, string UserName);
    }
}
