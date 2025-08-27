using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Tests.Objects;

public sealed class ConfigurationScope : IDisposable
{
    public Configuration Instance { get; }
    private readonly IServiceProvider? _prev;
    public ConfigurationScope(params (string Key, string? Value)[] overrides)
    : this(overrides: overrides.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase))
    { }
    public ConfigurationScope(
        IDictionary<string, string?>? defaults = null,
        IDictionary<string, string?>? overrides = null,
        Action<IServiceCollection>? addServices = null)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            // defaults for tests
            ["Ekom:PerStoreStock"] = "false",
            ["Ekom:ShareBasket"] = "false",
            ["Ekom:VatCalcRounding"] = "RoundToEven",
            ["Ekom:OrderVatCalcRounding"] = "None",
            ["Ekom:ExamineSearchIndex"] = "ExternalIndex",
            ["Ekom:BasketCookieLifetime"] = "360",
            ["Ekom:CategoryRootLevel"] = "3",
            ["Ekom:ReservationTimeout"] = "30",
            ["Ekom:CustomerData"] = "false",
            ["Ekom:DisableStock"] = "false",
            ["Ekom:VatRoundingScope"] = "PerUnit",
        };

        if (defaults != null)
            foreach (var kv in defaults) dict[kv.Key] = kv.Value;
        if (overrides != null)
            foreach (var kv in overrides) dict[kv.Key] = kv.Value;

        var cfgRoot = new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(cfgRoot);
        services.AddSingleton<Configuration>();

        addServices?.Invoke(services);

        var provider = services.BuildServiceProvider();
        _prev = Configuration.Resolver;
        Configuration.Resolver = provider;
        Instance = provider.GetRequiredService<Configuration>();
    }

    public void Dispose()
    {
        Configuration.Resolver = _prev;
    }
}
