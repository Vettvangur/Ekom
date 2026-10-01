using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Security;

namespace Ekom.Site.U17.EkomIntegration;

public sealed class MemberProductFactory(IHttpContextAccessor accessor) : IPerStoreFactory<IProduct>
{
    public IProduct Create(UmbracoContent item, IStore store) => new MemberProduct(item, store, accessor);
}

public sealed class MemberVariantFactory(IHttpContextAccessor accessor) : IPerStoreFactory<IVariant>
{
    public IVariant Create(UmbracoContent item, IStore store) => new MemberVariant(item, store, accessor);
}

public sealed class MemberProduct(UmbracoContent item, IStore store, IHttpContextAccessor accessor) : Product(item, store)
{
    public override List<IPrice> Prices => DisableDiscounts ? base.Prices
        : MemberPricing.Apply(base.Prices, Vat, Store.VatIncludedInPrice, accessor);
}

public sealed class MemberVariant(UmbracoContent item, IStore store, IHttpContextAccessor accessor) : Variant(item, store)
{
    public override List<IPrice> Prices => Product?.DisableDiscounts == true ? base.Prices
        : MemberPricing.Apply(base.Prices, Vat, Store.VatIncludedInPrice, accessor);
}

internal static class MemberPricing
{
    private static readonly OrderedDiscount MemberDiscount = new(
        new Guid("c1048d39-3db1-45dd-bf4f-68515d09d131"), "Member discount", false, 0.20m,
        DiscountType.Percentage, new List<string>(), new List<string>(), null, false, false);

    internal static List<IPrice> Apply(List<IPrice> prices, decimal vat, bool vatIncluded, IHttpContextAccessor accessor)
    {
        // Cached catalog objects must resolve member status from the current request, not construction time.
        var memberManager = accessor.HttpContext?.RequestServices.GetService<IMemberManager>();
        if (memberManager?.IsLoggedIn() != true) return prices;

        return prices.Select(price =>
        {
            if (price.OriginalValue <= 0) return price;
            var memberPrice = new Price(price.OriginalValue, price.Currency, vat, vatIncluded, MemberDiscount);
            return memberPrice.Value < price.Value ? memberPrice : price;
        }).ToList();
    }
}
