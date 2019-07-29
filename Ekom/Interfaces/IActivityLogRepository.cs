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
        Task<List<OrderActivityLog>> GetLatestActivityLogsOrdersAsync();
        Task<List<OrderActivityLog>> GetLatestActivityLogsOrdersByUserAsync(string userName);
        Task<List<OrderActivityLog>> GetLogsAsync(Guid uniqueId);
        Task<List<OrderActivityLog>> GetLogsAsync(string OrderNumber);
        Task InsertAsync(Guid Key, string Log, string UserName);
    }
}
