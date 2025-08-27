using Ekom.Models;
using Moq;
using System.Globalization;

namespace Ekom.Tests.Builders;

public class StoreBuilder
{
    private readonly Mock<IStore> _mock;

    private StoreBuilder()
    {
        _mock = new Mock<IStore>();
        WithAlias("IS");
        WithCulture("is-IS");
        WithVat(24);
        WithVatIncluded(true);
        WithCurrencies(new CurrencyModel {   CurrencyValue = "is-IS", CurrencyFormat = "C" });
    }

    public static StoreBuilder Create() => new StoreBuilder();

    public StoreBuilder WithAlias(string alias)
    {
        _mock.SetupGet(s => s.Alias).Returns(alias);
        return this;
    }

    public StoreBuilder WithCulture(string cultureName)
    {
        var ci = new CultureInfo(cultureName);
        _mock.SetupGet(s => s.Culture).Returns(ci);
        _mock.SetupGet(s => s.Cultures).Returns(new List<CultureInfo> { ci });
        return this;
    }

    /// pass VAT in percent, e.g. 24 => 0.24
    public StoreBuilder WithVat(decimal vatPercent)
    {
        _mock.SetupGet(s => s.Vat).Returns(vatPercent / 100m);
        return this;
    }

    public StoreBuilder WithVatIncluded(bool included)
    {
        _mock.SetupGet(s => s.VatIncludedInPrice).Returns(included);
        return this;
    }

    public StoreBuilder WithCurrencies(params CurrencyModel[] currencies)
    {
        _mock.SetupGet(s => s.Currencies).Returns(currencies.ToList());
        return this;
    }

    public StoreBuilder WithUrlPrefix(string prefix)
    {
        _mock.Setup(s => s.UrlPrefix(It.IsAny<string>())).Returns(prefix);
        return this;
    }

    public IStore Build() => _mock.Object;
}
