using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging;

namespace Ekom.Repositories;
class ActivityLogRepository
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

    public async Task InsertAsync(Guid Key, string Log, string UserName)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        await db.InsertAsync(new OrderActivityLog
        {
            UniqueID = Guid.NewGuid(),
            Key = Key,
            Log = Log,
            UserName = UserName,
            Date = DateTime.Now,
        }).ConfigureAwait(false);
    }

    public async Task<List<OrderActivityLog>> GetLatestActivityLogsOrdersByUserAsync(string userName)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        string sql = _databaseFactory.IsSqlite
            ? @"SELECT a.[UniqueId]
                  ,a.[Key]
                  ,a.[Log]
                  ,a.[UserName]
                  ,a.[DATE],
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
                  ,a.[DATE],
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
                        ,a.[DATE],
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
                        ,a.[DATE],
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
                      ,a.[DATE],
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
        return await db.FromSql<OrderActivityLog>(@"SELECT a.[UniqueId]
                        ,a.[Key]
                        ,a.[Log]
                        ,a.[UserName]
                        ,a.[DATE],
	                    b.orderNumber as OrderNumber
                    FROM [EkomOrdersActivityLog] a
                    left join EkomOrders b on b.UniqueId = a.[Key]
                    WHERE a.[Key] = @0
                    order by Date desc",
                uniqueId)
            .ToListAsync()
            .ConfigureAwait(false);
    }
}
