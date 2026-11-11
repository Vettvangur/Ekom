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

    [Fact]
    public void DateFormatComesFromTheStoreCulture()
    {
        var icelandic = ManagerDateFormat.From("is-IS");
        Assert.NotNull(icelandic);
        Assert.Equal("is-IS", icelandic.Culture);
        Assert.Equal(new DateTime(2026, 11, 5, 14, 7, 0).ToString("d", System.Globalization.CultureInfo.GetCultureInfo("is-IS")),
            new DateTime(2026, 11, 5, 14, 7, 0).ToString(icelandic.ShortDate, System.Globalization.CultureInfo.GetCultureInfo("is-IS")));
        Assert.Contains("H", icelandic.ShortTime, StringComparison.Ordinal);
        Assert.Equal(12, icelandic.MonthNames.Count);
        Assert.Equal(12, icelandic.AbbreviatedMonthNames.Count);
        Assert.Equal(7, icelandic.ShortestDayNames.Count);
        Assert.All(icelandic.MonthNames, name => Assert.False(string.IsNullOrWhiteSpace(name)));

        var american = ManagerDateFormat.From("en-US");
        Assert.NotNull(american);
        Assert.StartsWith("M", american.ShortDate, StringComparison.Ordinal);
        Assert.Contains("tt", american.ShortTime, StringComparison.Ordinal);
        Assert.Equal("January", american.MonthNames[0]);

        Assert.Null(ManagerDateFormat.From(null));
        Assert.Null(ManagerDateFormat.From(" "));
    }
}
