using Ekom.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System.Data.Common;
using System.Reflection;

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
        if (string.IsNullOrWhiteSpace(providerName))
        {
            providerName = configuration[$"ConnectionStrings:{providerNameKey}"];
        }

        if (string.IsNullOrWhiteSpace(providerName))
        {
            providerName = configuration[$"Umbraco:CMS:Global:ConnectionStrings:{providerNameKey}"];
        }

        _linqToDbProviderName = ResolveLinqToDbProviderName(providerName, _connectionString);
    }

    public DbContext GetDatabase() => new(_linqToDbProviderName, _connectionString);
    public string LinqToDbProviderName => _linqToDbProviderName;
    public bool IsSqlServer => _linqToDbProviderName == LinqToDB.ProviderName.SqlServer;
    public bool IsSqlite => IsSqliteProviderName(_linqToDbProviderName);

    public DbConnection GetDbConnection() => CreateDbConnection();

    public SqlConnection GetSqlConnection()
    {
        if (!IsSqlServer)
        {
            throw new InvalidOperationException("SQL Server connection requested for a non-SQL Server provider.");
        }

        return new SqlConnection(_connectionString);
    }

    static string ResolveLinqToDbProviderName(string? providerName, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            if (LooksLikeSqliteConnectionString(connectionString))
            {
                return ResolveSqliteProviderName();
            }

            return LinqToDB.ProviderName.SqlServer;
        }

        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveSqliteProviderName();
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

    static string ResolveSqliteProviderName()
    {
        const string sqliteMsFieldName = "SQLiteMS";
        FieldInfo? field = typeof(LinqToDB.ProviderName).GetField(sqliteMsFieldName, BindingFlags.Public | BindingFlags.Static);
        if (field?.GetValue(null) is string providerName && !string.IsNullOrWhiteSpace(providerName))
        {
            return providerName;
        }

        return LinqToDB.ProviderName.SQLite;
    }

    static bool IsSqliteProviderName(string providerName)
    {
        return providerName.StartsWith("SQLite", StringComparison.OrdinalIgnoreCase);
    }

    static bool LooksLikeSqliteConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        if (connectionString.Contains("Cache=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Foreign Keys=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Filename=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Mode=", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (connectionString.Contains("Data Source=|DataDirectory|", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains(".sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
