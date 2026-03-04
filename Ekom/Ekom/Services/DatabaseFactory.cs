using Ekom.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Data.Common;

namespace Ekom.Services;

public class DatabaseFactory
{
    readonly string _connectionString;
    readonly string _linqToDbProviderName;

    public DatabaseFactory(IConfiguration configuration)
    {
        const string connectionStringName = "umbracoDbDSN";
        const string providerNameKey = "umbracoDbDSN_ProviderName";
        _connectionString = configuration.GetConnectionString(connectionStringName);
        string? providerName = configuration.GetConnectionString(providerNameKey);
        _linqToDbProviderName = ResolveLinqToDbProviderName(providerName);
    }

    public DbContext GetDatabase() => new(_linqToDbProviderName, _connectionString);
    public string LinqToDbProviderName => _linqToDbProviderName;
    public bool IsSqlServer => _linqToDbProviderName == LinqToDB.ProviderName.SqlServer;
    public bool IsSqlite => _linqToDbProviderName == LinqToDB.ProviderName.SQLite;

    public DbConnection GetDbConnection() => CreateDbConnection();

    public SqlConnection GetSqlConnection()
    {
        if (!IsSqlServer)
        {
            throw new InvalidOperationException("SQL Server connection requested for a non-SQL Server provider.");
        }

        return new SqlConnection(_connectionString);
    }

    static string ResolveLinqToDbProviderName(string? providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return LinqToDB.ProviderName.SqlServer;
        }

        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return LinqToDB.ProviderName.SQLite;
        }

        if (providerName.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
        {
            return LinqToDB.ProviderName.SqlServer;
        }

        throw new InvalidOperationException($"Unsupported database provider '{providerName}'. Supported providers: Microsoft.Data.SqlClient, System.Data.SqlClient, Microsoft.Data.Sqlite.");
    }

    DbConnection CreateDbConnection()
    {
        if (IsSqlite)
        {
            return new SqliteConnection(_connectionString);
        }

        if (IsSqlServer)
        {
            return new SqlConnection(_connectionString);
        }

        throw new InvalidOperationException($"Unsupported LinqToDB provider '{_linqToDbProviderName}'.");
    }
}
