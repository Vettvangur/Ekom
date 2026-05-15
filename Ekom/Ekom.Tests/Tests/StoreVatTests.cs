using Ekom.Models;
using System.Globalization;
using Xunit;

namespace Ekom.Tests.Tests;

public class StoreVatTests
{
    [Theory]
    [InlineData("25.5", "0.255")]
    [InlineData("25", "0.25")]
    [InlineData("", "0")]
    [InlineData("25,5", "0")]
    public void Vat_Parses_With_Invariant_Culture(string vatValue, string expectedValue)
    {
        var store = new TestStore(vatValue);
        var expected = decimal.Parse(expectedValue, CultureInfo.InvariantCulture);

        Assert.Equal(expected, store.Vat);
    }

    private sealed class TestStore : Store
    {
        public TestStore(string vatValue)
        {
            _properties = new Dictionary<string, string>
            {
                ["vat"] = vatValue,
            };
        }
    }
}
