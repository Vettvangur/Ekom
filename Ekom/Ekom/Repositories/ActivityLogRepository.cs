using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging;

namespace Ekom.Repositories;
public class ActivityLogRepository
{
    readonly ILogger _logger;
    readonly DatabaseFactory _databaseFactory;

    public ActivityLogRepository(
        ILogger<ActivityLogRepository> logger,
        DatabaseFactory databaseFactory)
    {
        _logger = logger;
        _databaseFactory = databaseFactory;
    }

    public Task InsertAsync(Guid key, string log, string userName)
        => InsertAsync(
            new[]
            {
                new OrderActivityLogWrite(
                    key,
                    log,
                    userName,
                    DateTime.Now,
                    OrderActivityLogType.Info),
            },
            CancellationToken.None);

    public async Task InsertAsync(IReadOnlyCollection<OrderActivityLogWrite> items, CancellationToken ct = default)
    {
        if (items.Count == 0)
        {
            return;
        }

        await using DbContext db = _databaseFactory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync().ConfigureAwait(false);

        foreach (OrderActivityLogWrite item in items)
        {
            await db.InsertAsync(new OrderActivityLog
            {
                UniqueID = Guid.NewGuid(),
                Key = item.OrderId,
                Log = item.Message,
                UserName = item.UserName,
                Date = item.Date,
                LogType = item.LogType,
            }, token: ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<List<OrderActivityLog>> GetLatestActivityLogsOrdersByUserAsync(string userName)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        string sql = _databaseFactory.IsSqlite
            ? @"SELECT a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE]
                  ,a.[LogType],
  	              b.orderNumber as OrderNumber
              FROM [EkomOrdersActivityLog] a
              left join EkomOrders b on b.UniqueId = a.[Key]
              WHERE a.[UserName] = @0
              order by Date desc
              LIMIT 100"
            : @"SELECT TOP 100 a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE]
                  ,a.[LogType],
  	              b.orderNumber as OrderNumber
              FROM [EkomOrdersActivityLog] a
              left join EkomOrders b on b.UniqueId = a.[Key]
              WHERE a.[UserName] = @0
              order by Date desc";

        IQueryable<OrderActivityLog> queryResult = db.FromSql<OrderActivityLog>(sql, userName);

        return await queryResult
            .GroupBy(x => x.OrderNumber)
            .Select(x => x.First())
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<OrderActivityLog>> GetLatestActivityLogsOrdersAsync()
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        string sql = _databaseFactory.IsSqlite
            ? @"SELECT a.[UniqueId]
                        ,a.[Key]
                        ,a.[Log]
                        ,a.[UserName]
                        ,a.[DATE]
                        ,a.[LogType],
  	                    b.orderNumber as OrderNumber
                    FROM [EkomOrdersActivityLog] a
                    left join EkomOrders b on b.UniqueId = a.[Key]
                    WHERE UserName != 'Customer' AND UserName != ''
                    order by Date desc
                    LIMIT 100"
            : @"SELECT TOP 100 a.[UniqueId]
                        ,a.[Key]
                        ,a.[Log]
                        ,a.[UserName]
                        ,a.[DATE]
                        ,a.[LogType],
  	                    b.orderNumber as OrderNumber
                    FROM [EkomOrdersActivityLog] a
                    left join EkomOrders b on b.UniqueId = a.[Key]
                    WHERE UserName != 'Customer' AND UserName != ''
                    order by Date desc";

        IQueryable<OrderActivityLog> queryResult = db.FromSql<OrderActivityLog>(sql);

        return await queryResult
            .GroupBy(x => x.OrderNumber)
            .Select(x => x.First())
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<OrderActivityLog>> GetLogsAsync(string OrderNumber)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        return await db.FromSql<OrderActivityLog>(@"SELECT a.[UniqueId]
                      ,a.[Key]
                      ,a.[Log]
                      ,a.[UserName]
                      ,a.[DATE]
                      ,a.[LogType],
	                  b.orderNumber as OrderNumber
                  FROM [EkomOrdersActivityLog] a
                  left join EkomOrders b on b.UniqueId = a.[Key]
                  WHERE OrderNumber = @0
                  order by Date desc", OrderNumber)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<List<OrderActivityLog>> GetLogsAsync(Guid uniqueId)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        return await (
            from activityLog in db.OrderActivityLog
            from order in db.OrderData
                .LeftJoin(x => x.UniqueId == activityLog.Key)
            where activityLog.Key == uniqueId
            orderby activityLog.Date descending
            select new OrderActivityLog
            {
                UniqueID = activityLog.UniqueID,
                Key = activityLog.Key,
                Log = activityLog.Log,
                UserName = activityLog.UserName,
                Date = activityLog.Date,
                LogType = activityLog.LogType,
                OrderNumber = order != null ? order.OrderNumber : string.Empty,
            })
            .ToListAsync()
            .ConfigureAwait(false);
    }
}
