using Ekom.Printer.Agent.Client;
using Xunit;

namespace Ekom.Printer.Agent.Tests;

public sealed class AgentSettingsTests
{
    [Theory]
    [InlineData("http://shop.example")]
    [InlineData("https://user:password@shop.example")]
    [InlineData("https://shop.example?token=secret")]
    [InlineData("https://shop.example/#fragment")]
    [InlineData("relative/path")]
    public void RejectsUnsafeServerAddresses(string url) => Assert.Throws<ArgumentException>(() =>
        new AgentSettings(url, "warehouse", "Office printer", false).Validate());

    [Fact]
    public void AllowsHttpsSitePathAndRequiresPrinterAndStation()
    {
        var settings = new AgentSettings("https://shop.example/site", "warehouse", "Office printer", false);
        settings.Validate();
        Assert.Throws<ArgumentException>(() => (settings with { PrinterName = "" }).Validate());
        Assert.Throws<ArgumentException>(() => (settings with { StationAlias = " warehouse " }).Validate());
        Assert.Throws<ArgumentException>(() => (settings with { StationAlias = new string('a', 101) }).Validate());
    }
}
