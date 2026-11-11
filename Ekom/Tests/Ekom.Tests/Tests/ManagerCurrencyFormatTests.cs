using Ekom.Models.Manager;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerCurrencyFormatTests
{
    [Fact]
    public void UsesTheCultureConfiguredOnTheStore()
    {
        var format = ManagerCurrencyFormat.From("ISK", "is-IS");

        Assert.NotNull(format);
        Assert.Equal("ISK", format.Code);
        Assert.Equal(".", format.GroupSeparator);
        Assert.Equal(",", format.DecimalSeparator);
        Assert.Equal(0, format.DecimalDigits);
        Assert.Equal(3, format.PositivePattern);
        Assert.False(string.IsNullOrWhiteSpace(format.Symbol));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("USD")]
    [InlineData("not-a-culture")]
    public void FallsBackToACultureForTheCurrencyCode(string? culture)
    {
        var format = ManagerCurrencyFormat.From("USD", culture);

        Assert.NotNull(format);
        Assert.Equal(2, format.DecimalDigits);
        Assert.Contains("$", format.Symbol, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownCurrencyHasNoFormat()
    {
        Assert.Null(ManagerCurrencyFormat.From("ZZZ", null));
    }
}
