using Ekom.Tests.Objects;
using Ekom.Utilities;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ekom.Tests.Tests;

public class ConfigurationTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("false", false)]
    [InlineData("true", true)]
    public void ManagerOrderStatusEventDefaultPreservesOptionalValue(string? value, bool? expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ekom:Manager:OrderStatusFireEventsByDefault"] = value,
            })
            .Build();
        var options = new EkomOptions();
        configuration.GetSection("Ekom").Bind(options);

        Assert.Equal(expected, new Configuration(configuration).ManagerOrderStatusFireEventsByDefault);
        Assert.Equal(expected, options.Manager.OrderStatusFireEventsByDefault);
    }

    [Fact]
    public void Reads_From_InMemory_Appsettings_And_Overrides()
    {
        using var configScope = new ConfigurationScope(
            ("Ekom:PerStoreStock", "true"),
            ("Ekom:VatCalcRounding", "RoundUp")
        );

        Assert.True(Configuration.Instance.PerStoreStock);
        Assert.Equal(Rounding.RoundUp, Configuration.Instance.VatCalculationRounding);
        Assert.Equal("ExternalIndex", Configuration.Instance.ExamineSearchIndex); // from defaults
    }
}
