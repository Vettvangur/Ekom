using Ekom.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Ekom.Services;

public class DatabaseFactory
{
    readonly string _connectionString;

    public DatabaseFactory(IConfiguration configuration)
    {
        const string connectionStringName = "umbracoDbDSN";
        _connectionString = configuration.GetConnectionString(connectionStringName);
    }

    public DbContext GetDatabase() => new(_connectionString);
    public SqlConnection GetSqlConnection() => new(_connectionString);
}
