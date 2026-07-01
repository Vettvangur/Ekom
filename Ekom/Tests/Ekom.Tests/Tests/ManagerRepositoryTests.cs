using Ekom;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerRepositoryTests
{
    [Fact]
    public void GenerateWhereClause_IncludesUniqueIdForFullGuidQuery()
    {
        var repository = CreateRepository();
        string query = Guid.NewGuid().ToString();

        string whereClause = GenerateWhereClause(repository, query);

        Assert.Contains("UniqueId = @queryUniqueId", whereClause, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateWhereClause_DoesNotIncludeUniqueIdForPartialGuidQuery()
    {
        var repository = CreateRepository();

        string whereClause = GenerateWhereClause(repository, "abc123");

        Assert.DoesNotContain("UniqueId = @queryUniqueId", whereClause, StringComparison.Ordinal);
    }

    private static string GenerateWhereClause(ManagerRepository repository, string query)
    {
        MethodInfo method = typeof(ManagerRepository).GetMethod(
            "GenerateWhereClause",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (string)method.Invoke(repository, new object[]
        {
            string.Empty,
            query,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
        })!;
    }

    private static ManagerRepository CreateRepository()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = "Data Source=:memory:",
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            })
            .Build();

        return new ManagerRepository(
            NullLogger<ManagerRepository>.Instance,
            new Configuration(configuration),
            new DatabaseFactory(configuration, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory)),
            Mock.Of<IStoreService>());
    }
}
