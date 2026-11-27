using Ekom.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Ekom.Brevo.Tests;

internal sealed class BrevoTestDatabase : IDisposable
{
    private readonly bool _ownsFile;
    public string Path { get; }
    public DatabaseFactory Factory { get; }

    public BrevoTestDatabase(string? path = null)
    {
        _ownsFile = path == null;
        Path = path ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"brevo-{Guid.NewGuid():N}.sqlite");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={Path};Pooling=False;Default Timeout=30",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
        }).Build();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(System.IO.Path.GetTempPath());
        Factory = new DatabaseFactory(configuration, environment.Object);
    }

    public void Dispose()
    {
        if (_ownsFile && File.Exists(Path)) File.Delete(Path);
    }
}
