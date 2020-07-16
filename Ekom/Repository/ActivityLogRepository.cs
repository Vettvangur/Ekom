using Ekom.Interfaces;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Scoping;

namespace Ekom.Repository
{
    class ActivityLogRepository : IActivityLogRepository
    {
        readonly IScopeProvider _scopeProvider;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="scopeProvider"></param>
        public ActivityLogRepository(IScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        public async Task InsertAsync(Guid Key, string Log, string UserName)
        {
            using (var db = _scopeProvider.CreateScope())
            {
                await db.Database.InsertAsync(new OrderActivityLog
                {
                    UniqueID = Guid.NewGuid(),
                    Key = Key,
                    Log = Log,
                    UserName = UserName,
                    Date = DateTime.Now,
                }).ConfigureAwait(false);

                db.Complete();
            }
        }

        public async Task<List<OrderActivityLog>> GetLatestActivityLogsOrdersByUserAsync(string userName)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true))
            {
                var queryResult = await db.Database.QueryAsync<OrderActivityLog>(@"SELECT TOP 100 a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE],
	              b.orderNumber as OrderNumber
              FROM [EkomOrdersActivityLog] a
              left join EkomOrders b on b.UniqueId = a.[Key]
              WHERE a.[UserName] = @0
              order by Date desc", userName)
                    .ConfigureAwait(false);


                return queryResult.DistinctBy(x => x.OrderNumber).ToList();
            }
        }

        public async Task<List<OrderActivityLog>> GetLatestActivityLogsOrdersAsync()
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true))
            {
                var queryResult = await db.Database.QueryAsync<OrderActivityLog>(
                    @"SELECT TOP 100 a.[UniqueId]
                      ,a.[Key]
                      ,a.[Log]
                      ,a.[UserName]
                      ,a.[DATE],
	                  b.orderNumber as OrderNumber
                  FROM [EkomOrdersActivityLog] a
                  left join EkomOrders b on b.UniqueId = a.[Key]
                  WHERE UserName != 'Customer' AND UserName != ''
                  order by Date desc")
                    .ConfigureAwait(false);

                return queryResult.DistinctBy(x => x.OrderNumber).ToList();
            }
        }

        public async Task<List<OrderActivityLog>> GetLogsAsync(string OrderNumber)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true))
            {
                return await db.Database.FetchAsync<OrderActivityLog>(@"SELECT a.[UniqueId]
                      ,a.[Key]
                      ,a.[Log]
                      ,a.[UserName]
                      ,a.[DATE],
	                  b.orderNumber as OrderNumber
                  FROM [EkomOrdersActivityLog] a
                  left join EkomOrders b on b.UniqueId = a.[Key]
                  WHERE OrderNumber = @0
                  order by Date desc", OrderNumber)
                    .ConfigureAwait(false);
            }
        }

        public async Task<List<OrderActivityLog>> GetLogsAsync(Guid uniqueId)
        {
            using (var db = _scopeProvider.CreateScope(autoComplete: true))
            {
                return await db.Database.FetchAsync<OrderActivityLog>(@"SELECT a.[UniqueId]
                      ,a.[Key]
                      ,a.[Log]
                      ,a.[UserName]
                      ,a.[DATE],
	                  b.orderNumber as OrderNumber
                  FROM [EkomOrdersActivityLog] a
                  left join EkomOrders b on b.UniqueId = a.[Key]
                  WHERE a.[Key] = @0
                  order by Date desc", uniqueId)
                    .ConfigureAwait(false);
            }
        }
    }
}
