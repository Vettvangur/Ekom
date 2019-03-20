using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;

namespace Ekom.Repository
{
    public class ActivityLogRepository : IActivityLogRepository
    {
        ApplicationContext _appCtx;

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="appCtx "></param>
        public ActivityLogRepository(ApplicationContext appCtx)
        {
            _appCtx = appCtx;
        }

        public void Insert(Guid Key, string Log, string UserName)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Insert(new OrderActivityLog()
                {
                    UniqueID = Guid.NewGuid(),
                    Key = Key,
                    Log = Log,
                    UserName = UserName,
                    Date = DateTime.Now
                });

            }
        }

        public IEnumerable<OrderActivityLog> GetLatestActivityLogsOrdersByUser(string userName)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<OrderActivityLog>(@"SELECT TOP 100 a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE],
	              b.orderNumber as OrderNumber
              FROM [EkomOrdersActivityLog] a
              left join EkomOrders b on b.UniqueId = a.[Key]
              WHERE a.[UserName] = @0
              order by Date desc", userName).DistinctBy(x => x.OrderNumber);
            }
        }

        public IEnumerable<OrderActivityLog> GetLatestActivityLogsOrders()
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<OrderActivityLog>(@"SELECT TOP 100 a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE],
	              b.orderNumber as OrderNumber
              FROM [EkomOrdersActivityLog] a
              left join EkomOrders b on b.UniqueId = a.[Key]
              WHERE UserName != 'Customer' AND UserName != ''
              order by Date desc").DistinctBy(x => x.OrderNumber);
            }
        }

        public IEnumerable<OrderActivityLog> GetLogs(string OrderNumber)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<OrderActivityLog>(@"SELECT a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE],
	              b.orderNumber as OrderNumber
              FROM [EkomOrdersActivityLog] a
              left join EkomOrders b on b.UniqueId = a.[Key]
              WHERE OrderNumber = @0
              order by Date desc", OrderNumber);
            }
        }

        public IEnumerable<OrderActivityLog> GetLogs(Guid uniqueId)
        {
            using (var db = ApplicationContext.Current.DatabaseContext.Database)
            {
                return db.Query<OrderActivityLog>(@"SELECT a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE],
	              b.orderNumber as OrderNumber
              FROM [EkomOrdersActivityLog] a
              left join EkomOrders b on b.UniqueId = a.[Key]
              WHERE a.[Key] = @0
              order by Date desc", uniqueId);
            }
        }
    }
}
