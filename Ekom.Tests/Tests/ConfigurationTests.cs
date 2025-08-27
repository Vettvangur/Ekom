using Ekom.Tests.Objects;
using Ekom.Utilities;
using Xunit;

namespace Ekom.Tests.Tests;

public class ConfigurationTests
{
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
