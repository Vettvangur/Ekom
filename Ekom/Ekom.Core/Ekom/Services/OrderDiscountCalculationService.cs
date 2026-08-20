using Ekom.API;
using Ekom.Cache;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;

namespace Ekom.Services;

public sealed class OrderDiscountCalculationService : IOrderDiscountCalculationService
{
    private readonly Catalog _catalog;
    private readonly ICouponCache _couponCache;
    private readonly DiscountCache _discountCache;
    private readonly INodeService _nodeService;
    private readonly IStoreService _storeService;
    private readonly OrderDiscountCalculationContextAccessor _contextAccessor;

    internal OrderDiscountCalculationService(
        Catalog catalog,
        ICouponCache couponCache,
        DiscountCache discountCache,
        INodeService nodeService,
        IStoreService storeService,
        OrderDiscountCalculationContextAccessor contextAccessor)
    {
        _catalog = catalog;
        _couponCache = couponCache;
        _discountCache = discountCache;
        _nodeService = nodeService;
        _storeService = storeService;
        _contextAccessor = contextAccessor;
    }

    public async Task<OrderDiscountCalculationResult> CalculateByCouponAsync(
        OrderDiscountCalculationRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.CouponCode))
        {
            throw new ArgumentException("string.IsNullOrWhiteSpace", nameof(request.CouponCode));
        }

        if (request.Lines == null || request.Lines.Count == 0)
        {
            throw new ArgumentException("No order lines provided", nameof(request.Lines));
        }

        var store = ResolveStore(request.StoreAlias);
        var couponCode = request.CouponCode.ToLowerInvariant();
        var discount = ResolveDiscount(couponCode, store.Alias);
        ValidateClientLineIds(request.Lines);

        var calculation = await CreateOrderInfoAsync(request.Lines, store, ct).ConfigureAwait(false);
        var orderInfo = calculation.OrderInfo;
        var lineSnapshots = calculation.Lines;
        var couponLineTargets = orderInfo.orderLines
            .Select(line => DiscountApplicability.MatchesLineTargets(line, discount, _nodeService))
            .ToArray();

        var messages = new List<string>();
        var orderDiscountConstraintsMet = DiscountApplicability.AreOrderConstraintsMet(orderInfo, discount);

        OrderedDiscount? orderedDiscount = null;
        if (orderDiscountConstraintsMet)
        {
            orderedDiscount = new OrderedDiscount(discount);
            orderInfo.Discount = orderedDiscount;
            orderInfo.Coupon = couponCode;
        }
        else
        {
            messages.Add("Discount constraints are not valid for the supplied order lines.");
        }

        var lineResults = new List<OrderDiscountCalculationLineResult>();
        for (var index = 0; index < orderInfo.orderLines.Count; index++)
        {
            var line = orderInfo.orderLines[index];
            var couponApplicable = orderDiscountConstraintsMet && couponLineTargets[index];
            var amount = line.Amount;
            var lineTotalBeforeDiscount = lineSnapshots[index].LineTotalBeforeDiscount;
            var lineTotalAfterDiscount = amount.Value;
            var discountAmount = Math.Max(0, lineTotalBeforeDiscount - lineTotalAfterDiscount);
            var couponSelected = couponApplicable
                && orderedDiscount != null
                && line.Discount?.Key == orderedDiscount.Key;
            var discountApplied = couponSelected && discountAmount > 0;

            lineResults.Add(new OrderDiscountCalculationLineResult
            {
                ClientLineId = lineSnapshots[index].RequestLine.ClientLineId,
                Sku = line.Product.SKU,
                VariantSku = line.Variant?.SKU,
                Quantity = line.Quantity,
                CouponApplicable = couponApplicable,
                UnitPriceBeforeDiscount = line.Quantity == 0 ? 0 : lineTotalBeforeDiscount / line.Quantity,
                LineTotalBeforeDiscount = lineTotalBeforeDiscount,
                DiscountAmount = discountAmount,
                LineTotalAfterDiscount = lineTotalAfterDiscount,
                Vat = amount.Vat.Value,
                DiscountApplied = discountApplied,
            });
        }

        var hasApplicableLines = lineResults.Any(line => line.CouponApplicable);
        var hasAppliedLines = lineResults.Any(line => line.DiscountApplied && line.DiscountAmount > 0);

        return new OrderDiscountCalculationResult
        {
            Applied = orderDiscountConstraintsMet && hasAppliedLines,
            OrderConstraintsMet = orderDiscountConstraintsMet,
            HasApplicableLines = hasApplicableLines,
            CouponCode = couponCode,
            DiscountId = discount.Key,
            DiscountTitle = discount.Title,
            Currency = store.Currency.CurrencyValue,
            SubTotal = lineSnapshots.Sum(line => line.LineTotalBeforeDiscount),
            DiscountTotal = lineResults.Sum(line => line.DiscountAmount),
            GrandTotal = lineResults.Sum(line => line.LineTotalAfterDiscount),
            Lines = lineResults,
            Messages = messages,
        };
    }

    private IStore ResolveStore(string? storeAlias)
    {
        var store = !string.IsNullOrWhiteSpace(storeAlias)
            ? _storeService.GetStoreByAlias(storeAlias)
            : _storeService.GetStoreFromCache();

        if (store == null)
        {
            throw new ArgumentException("Unable to resolve store", nameof(storeAlias));
        }

        return store;
    }

    private IDiscount ResolveDiscount(string couponCode, string storeAlias)
    {
        if (!_couponCache.Cache.TryGetValue(couponCode, out CouponData? couponData))
        {
            throw new DiscountNotFoundException($"Unable to find couponCode {couponCode}");
        }

        if (couponData.NumberAvailable <= 0)
        {
            throw new DiscountHasNoUsageException("Coupon has no usage.");
        }

        if (_discountCache.Cache.TryGetValue(storeAlias, out var discounts)
            && discounts.TryGetValue(couponData.DiscountId, out IDiscount? discount))
        {
            return discount;
        }

        throw new DiscountUnableToFindCouponException($"Unable to find discount with coupon {couponCode}");
    }

    private static void ValidateClientLineIds(IReadOnlyList<OrderDiscountCalculationLineRequest> lines)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in lines)
        {
            if (line.ClientLineId == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line.ClientLineId))
            {
                throw new ArgumentException("ClientLineId cannot be blank", nameof(lines));
            }

            if (!ids.Add(line.ClientLineId))
            {
                throw new ArgumentException("Duplicate ClientLineId values are not allowed", nameof(lines));
            }
        }
    }

    private async Task<OrderDiscountCalculationOrderInfo> CreateOrderInfoAsync(
        IReadOnlyList<OrderDiscountCalculationLineRequest> lines,
        IStore store,
        CancellationToken ct)
    {
        var orderInfo = new OrderInfo(
            new OrderData
            {
                UniqueId = Guid.NewGuid(),
                OrderNumber = string.Empty,
                OrderStatus = OrderStatus.Pending,
                OrderInfo = string.Empty,
                CustomerEmail = string.Empty,
                CustomerUsername = string.Empty,
                ShippingCountry = string.Empty,
                Currency = store.Alias,
                StoreAlias = store.Alias,
            },
            store);
        var snapshots = new List<OrderDiscountCalculationLineSnapshot>();

        foreach (var requestLine in lines)
        {
            if (string.IsNullOrWhiteSpace(requestLine.Sku))
            {
                throw new ArgumentException("Line sku can not be empty", nameof(lines));
            }

            if (requestLine.Quantity <= 0)
            {
                throw new ArgumentException("Line quantity must be greater than zero", nameof(lines));
            }

            using var contextScope = _contextAccessor.Activate(requestLine.PricingContext);

            var product = await _catalog.GetProductAsync(requestLine.Sku, store.Alias, ct: ct).ConfigureAwait(false)
                ?? throw new ProductNotFoundException($"Unable to find product with sku: {requestLine.Sku}");
            var variant = await ResolveVariantAsync(requestLine, product, store.Alias, ct).ConfigureAwait(false);
            OrderLineVariantValidator.Validate(product, variant);

            var orderLine = new OrderLine(
                product,
                requestLine.Quantity,
                Guid.NewGuid(),
                orderInfo,
                new Dictionary<string, string>(),
                variant);

            orderInfo.orderLines.Add(orderLine);
            snapshots.Add(CreateLineSnapshot(requestLine, orderLine));
        }

        return new OrderDiscountCalculationOrderInfo(orderInfo, snapshots);
    }

    private static OrderDiscountCalculationLineSnapshot CreateLineSnapshot(
        OrderDiscountCalculationLineRequest requestLine,
        OrderLine orderLine)
    {
        var productOnlyAmount = orderLine.Amount;

        return new OrderDiscountCalculationLineSnapshot(
            requestLine,
            productOnlyAmount.Value);
    }

    private async Task<IVariant?> ResolveVariantAsync(
        OrderDiscountCalculationLineRequest requestLine,
        IProduct product,
        string storeAlias,
        CancellationToken ct)
    {
        IVariant? variant = null;

        if (requestLine.VariantKey.HasValue)
        {
            variant = await _catalog.GetVariantAsync(requestLine.VariantKey.Value, storeAlias, ct).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(requestLine.VariantSku))
        {
            variant = await _catalog.GetVariantAsync(requestLine.VariantSku, storeAlias, ct).ConfigureAwait(false);
        }

        if (variant == null && (requestLine.VariantKey.HasValue || !string.IsNullOrWhiteSpace(requestLine.VariantSku)))
        {
            throw new ProductNotFoundException($"Unable to find variant for sku: {requestLine.Sku}");
        }

        if (variant != null && variant.ProductKey != product.Key)
        {
            throw new ProductNotFoundException($"Variant does not belong to product with sku: {requestLine.Sku}");
        }

        return variant;
    }

    private sealed record OrderDiscountCalculationOrderInfo(
        OrderInfo OrderInfo,
        IReadOnlyList<OrderDiscountCalculationLineSnapshot> Lines);

    private sealed record OrderDiscountCalculationLineSnapshot(
        OrderDiscountCalculationLineRequest RequestLine,
        decimal LineTotalBeforeDiscount);

}
