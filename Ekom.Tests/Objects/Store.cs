using Ekom.Models;
using Ekom.Tests.Builders;

namespace Ekom.Tests.Objects;

public static class Stores
{
    public static IStore Store_IS_24Vat_VatIncluded =>
        StoreBuilder.Create()
            .WithVat(24)
            .WithVatIncluded(true)
            .Build();

    public static IStore Store_IS_24Vat_VatExcluded =>
        StoreBuilder.Create()
            .WithVat(24)
            .WithVatIncluded(false)
            .Build();

    public static IStore Store_IS_0Vat_VatIncluded =>
    StoreBuilder.Create()
        .WithVat(0)
        .WithVatIncluded(true)
        .Build();

    public static IStore Store_US_11Vat_VatIncluded =>
        StoreBuilder.Create()
            .WithAlias("US")
            .WithCulture("en-US")
            .WithCurrencies(new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" })
            .WithVat(11)
            .WithVatIncluded(true)
            .Build();

    public static IStore Store_US_11Vat_VatExcluded =>
        StoreBuilder.Create()
            .WithAlias("US")
            .WithCulture("en-US")
            .WithCurrencies(new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" })
            .WithVat(11)
            .WithVatIncluded(false)
            .Build();

    public static IStore Store_US_0Vat_VatIncluded =>
        StoreBuilder.Create()
            .WithAlias("US")
            .WithCulture("en-US")
            .WithCurrencies(new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" })
            .WithVat(0)
            .WithVatIncluded(true)
            .Build();
}
