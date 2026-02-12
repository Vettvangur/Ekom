using Ekom.API;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

partial class OrderService
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<bool> ApplyDiscountToOrderAsync(
        IDiscount discount,
        string? storeAlias = null,
        DiscountOrderSettings? settings = null,
        CancellationToken ct = default
    )
    {
        if (settings == null)
        {
            settings = new DiscountOrderSettings();
        }

        if (string.IsNullOrEmpty(storeAlias))
        {
            storeAlias = API.Store.Instance.GetStore()?.Alias;
        }

        if (string.IsNullOrEmpty(storeAlias))
        {
            return false;
        }

        var orderInfo = await GetOrderAsync(storeAlias, ct).ConfigureAwait(false);

        if (orderInfo == null)
        {
            return false;
        }

        if (orderInfo.Discount?.Key == discount.Key)
        {
            // throwing an exception allows callers to differentiate between an attempt to apply a worse discount
            // and a duplicate discount application
            // This can then be handled in api controllers or frontend code to display the appropriate error.

            // This was previously inside IsBetterDiscount which is incompatible with automatic global discounts
            throw new DiscountDuplicateException($"Can't add the same discount to order twice.");
        }

        SemaphoreSlim semaphore = GetOrderLock(orderInfo);
        if (!settings.IsEventHandler)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
        }
        try
        {
            if (ApplyDiscountToOrder(discount, orderInfo, settings))
            {
                if (settings.UpdateOrder)
                {
                    await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent, ct: ct)
                        .ConfigureAwait(false);
                }

                return true;
            }

            return false;
        }
        finally
        {
            if (!settings.IsEventHandler)
            {
                semaphore.Release();
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private bool ApplyDiscountToOrder(
        IDiscount discount,
        OrderInfo orderInfo,
        DiscountOrderSettings settings
    )
    {
        if (discount is IProductDiscount)
        {
            // This is not correct usage of an IProductDiscount, 
            // they should be automatically applied on OrderLine creation or use
            // ApplyDiscountToOrderLineProductAsync
            throw new NotSupportedException(
                "Ekom does not currently support comparing or applying ProductDiscounts to OrderInfo, IProductDiscount however inherits from IDiscount for simplicities sake"
            );
        }

        if (IsDiscountApplicable(orderInfo, discount) && IsBetterDiscount(orderInfo, discount))
        {
            // Remove worse coupons from orderlines
            foreach (OrderLine line in orderInfo.OrderLines.Where(line => line.Discount != null))
            {
                if (!discount.Stackable || IsBetterDiscount(line, discount))
                {
                    line.Discount = null;
                    line.Coupon = null;
                }
            }

            orderInfo.Discount = new OrderedDiscount(discount);
            orderInfo.Coupon = settings.Coupon;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Does not remove global discounts currently
    /// </summary>
    public async Task RemoveDiscountFromOrderAsync(string storeAlias, CancellationToken ct = default, DiscountOrderSettings? settings = null)
    {
        if (settings == null)
        {
            settings = new DiscountOrderSettings();
        }

        var orderInfo = await GetOrderAsync(storeAlias, ct).ConfigureAwait(false);

        SemaphoreSlim semaphore = GetOrderLock(orderInfo);
        if (!settings.IsEventHandler)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
        }
        try
        {
            RemoveDiscountFromOrder(orderInfo);
            if (settings.UpdateOrder)
            {
                await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent, ct: ct)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            if (!settings.IsEventHandler)
            {
                semaphore.Release();
            }
        }
    }
    private void RemoveDiscountFromOrder(OrderInfo orderInfo)
    {
        orderInfo.Discount = null;
        orderInfo.Coupon = null;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="ProductNotFoundException"></exception>
    /// <exception cref="OrderLineNotFoundException"></exception>
    /// <returns></returns>
    public async Task<bool> ApplyDiscountToOrderLineProductAsync(
        Guid productKey,
        IDiscount discount,
        string storeAlias,
        DiscountOrderSettings settings = null
    )
    {
        if (settings == null)
        {
            settings = new DiscountOrderSettings();
        }

        var product = await Catalog.Instance.GetProductAsync(productKey, storeAlias);

        if (product == null)
        {
            throw new ProductNotFoundException($"Unable to find product: {productKey}");
        }

        return await ApplyDiscountToOrderLineProductAsync(
            product,
            discount,
            storeAlias,
            settings
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Manually set coupon code on order, does not validate coupon or use other discount functionality
    /// </summary>
    public async Task SetCouponCodeAsync(string couponCode, string? storeAlias, DiscountOrderSettings? settings = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(couponCode))
        {
            return;
        }

        var orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

        if (orderInfo == null)
        {
            return;
        }

        if (orderInfo.Coupon != couponCode)
        {
            orderInfo.Coupon = couponCode;

            await UpdateOrderAndOrderInfoAsync(orderInfo, fireOnOrderUpdatedEvents: settings?.FireOnOrderUpdatedEvent ?? true, ct: ct).ConfigureAwait(false);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="ProductNotFoundException"></exception>
    /// <exception cref="OrderLineNotFoundException"></exception>
    /// <returns></returns>
    public async Task<bool> ApplyDiscountToOrderLineProductAsync(
        IProduct product,
        IDiscount discount,
        string storeAlias,
        DiscountOrderSettings settings = null
    )
    {
        if (settings == null)
        {
            settings = new DiscountOrderSettings();
        }

        OrderInfo orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

        SemaphoreSlim semaphore = GetOrderLock(orderInfo);
        if (!settings.IsEventHandler)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
        }
        try
        {
            OrderLine? orderLine
                = orderInfo.OrderLines.FirstOrDefault(line => line.Product.Key == product.Key)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line with product key: {product.Key}");
            }

            return await ApplyDiscountToOrderLineAsync(
                orderLine,
                discount,
                orderInfo,
                settings
            ).ConfigureAwait(false);
        }
        finally
        {
            if (!settings.IsEventHandler)
            {
                semaphore.Release();
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="OrderLineNotFoundException"></exception>
    /// <returns></returns>
    public async Task<bool> ApplyDiscountToOrderLineAsync(
        Guid lineKey,
        IDiscount discount,
        string storeAlias,
        DiscountOrderSettings? settings = null,
        CancellationToken ct = default
    )
    {
        if (settings == null)
        {
            settings = new DiscountOrderSettings();
        }

        var orderInfo = await GetOrderAsync(storeAlias, ct).ConfigureAwait(false);

        SemaphoreSlim semaphore = GetOrderLock(orderInfo);
        if (!settings.IsEventHandler)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
        }
        try
        {
            OrderLine? orderLine
                = orderInfo.OrderLines.FirstOrDefault(line => line.Key == lineKey)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {lineKey}");
            }

            return await ApplyDiscountToOrderLineAsync(
                orderLine,
                discount,
                orderInfo,
                settings,
                ct: ct
            ).ConfigureAwait(false);
        }
        finally
        {
            if (!settings.IsEventHandler)
            {
                semaphore.Release();
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="OrderLineNotFoundException"></exception>
    /// <returns></returns>
    private async Task<bool> ApplyDiscountToOrderLineAsync(
        OrderLine orderLine,
        IDiscount discount,
        OrderInfo orderInfo,
        DiscountOrderSettings? settings = null,
        CancellationToken ct = default
    )
    {
        _logger.LogDebug("Applying discount to orderline");

        if (settings == null)
        {
            settings = new DiscountOrderSettings();
        }

        if (IsDiscountApplicable(orderInfo, orderLine, discount))
        {
            // If a discount is applied to the OrderLine, 
            // assume that discount was better than thecurrent OrderInfo discount. 
            // (We have checks in place that make sure that stays true)
            if (orderLine.Discount != null)
            {
                if (IsBetterDiscount(orderLine, discount))
                {
                    orderLine.Discount = new OrderedDiscount(discount);
                    orderLine.Coupon = settings.Coupon;

                    if (settings.UpdateOrder)
                    {
                        await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent, ct: ct)
                            .ConfigureAwait(false);
                    }

                    _logger.LogDebug("Successfully applied discount to orderline");
                    return true;
                }
            }
            else
            {
                // Apply cart discount on line for comparison with new discount
                // was null so we are never overriding
                orderLine.Discount = orderInfo.Discount;

                if ((orderInfo.Discount == null || orderInfo.Discount.Stackable)
                && IsBetterDiscount(orderLine, discount))
                {
                    orderLine.Discount = new OrderedDiscount(discount);
                    orderLine.Coupon = settings.Coupon;

                    if (settings.UpdateOrder)
                    {
                        await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent, ct: ct)
                            .ConfigureAwait(false);
                    }

                    _logger.LogDebug("Successfully applied discount to orderline");
                    return true;
                }
                // When we add a new OrderLine, it might have an applicable ProductDiscount
                // If the OrderInfo has an exclusive discount we check if the total order price goes down
                // on applying the ProductDiscount, if so we throw away the OrderInfo discount and use the ProductDiscount instead.
                else if (orderInfo.Discount?.Stackable == false && IsBetterDiscount(orderInfo, discount))
                {
                    // It's possible that there exist previous OrderLine's that the ProductDiscount applies to
                    // in that case we assume this new orderline tipped the calculation in favor of this given ProductDiscount
                    // and that the older lines are missing this new about to be applied ProductDiscount (since the OrderInfo one was inclusive)
                    foreach (OrderLine line in orderInfo.orderLines)
                    {
                        if (IsDiscountApplicable(orderInfo, line, discount))
                        {
                            line.Discount = new OrderedDiscount(discount);
                        }
                    }

                    orderInfo.Discount = null;
                    _logger.LogDebug("Replaced exclusive OrderInfo discount with a ProductDiscount");
                    return true;
                }
                // Only other case is a worse discount

                orderLine.Discount = null;
            }
        }

        return false;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="OrderLineNotFoundException"></exception>
    /// <returns></returns>
    public async Task RemoveDiscountFromOrderLineAsync(
        Guid productKey,
        string storeAlias,
        DiscountOrderSettings? settings = null,
        CancellationToken ct = default)
    {
        if (settings == null)
        {
            settings = new DiscountOrderSettings();
        }

        OrderInfo orderInfo = await GetOrderAsync(storeAlias).ConfigureAwait(false);

        SemaphoreSlim semaphore = GetOrderLock(orderInfo);
        if (!settings.IsEventHandler)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
        }
        try
        {
            OrderLine? orderLine
                = orderInfo.OrderLines.FirstOrDefault(line => line.Product.Key == productKey)
                as OrderLine;

            if (orderLine == null)
            {
                throw new OrderLineNotFoundException($"Unable to find order line: {productKey}");
            }

            RemoveDiscountFromOrderLine(orderLine);

            if (settings.UpdateOrder)
            {
                await UpdateOrderAndOrderInfoAsync(orderInfo, settings.FireOnOrderUpdatedEvent, ct: ct)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            if (!settings.IsEventHandler)
            {
                semaphore.Release();
            }
        }
    }
    private void RemoveDiscountFromOrderLine(OrderLine orderLine)
    {
        if (orderLine == null)
        {
            throw new ArgumentException(nameof(OrderLine));
        }

        orderLine.Discount = null;
        orderLine.Coupon = null;
    }

    private bool IsBetterDiscount(OrderInfo orderInfo, IDiscount discount)
    {
        // Why don't we assume something is better than nothing ?
        // Possibly for orders where all OrderLine have ProductDiscount,
        // in those cases the ChargedAmount will stay the same.
        //if (orderInfo.Discount == null && !discount.Stackable && !discount.GlobalDiscount)
        //{
        //    decimal oldTotal = orderInfo.ChargedAmount.Value;

        //    orderInfo.Discount = new OrderedDiscount(discount);

        //    bool result = orderInfo.ChargedAmount.Value < oldTotal;

        //    orderInfo.Discount = null;

        //    return result;
        //}

        if (orderInfo.Discount == null)
        {
            return true;
        }

        if (discount is IProductDiscount productDiscount)
        {
            decimal oldTotal = orderInfo.ChargedAmount.Value;

            // Save original discounts
            OrderedDiscount prevOrderDiscount = orderInfo.Discount;
            List<OrderedDiscount> prevDiscounts = new List<OrderedDiscount>();
            foreach (OrderLine line in orderInfo.orderLines)
            {
                prevDiscounts.Add(line.Discount);

                if (IsDiscountApplicable(orderInfo, line, productDiscount))
                {
                    line.Discount = new OrderedDiscount(productDiscount);
                }
            }
            // In case of an exclusive discount, we remove since OrderInfo ChargedAmount ignores
            // product discounts when an exclusive order discount is applied.
            // This ignoring happens for comparison reasons and is explained in ChargedAmount.
            orderInfo.Discount = null;
            // Compare
            bool result = orderInfo.ChargedAmount.Value < oldTotal;

            // Reset to previous discounts
            orderInfo.Discount = prevOrderDiscount;
            for (int x = 0; x < orderInfo.OrderLines.Count; x++)
            {
                orderInfo.orderLines[x].Discount = prevDiscounts.ElementAt(x);
            }

            return result;
        }
        else
        {
            // In case of comparing an Exclusive to an inclusive discount, this simple CompareTo
            // does not apply
            //if (orderInfo.Discount.Type == discount.Type)
            //{
            //    return discount.CompareTo(orderInfo.Discount) > 0;
            //}

            OrderedDiscount oldDiscount = orderInfo.Discount;
            decimal oldTotal = orderInfo.ChargedAmount.Value;

            orderInfo.Discount = new OrderedDiscount(discount);

            bool result = orderInfo.ChargedAmount.Value < oldTotal;

            orderInfo.Discount = oldDiscount;

            return result;
        }
    }

    private bool IsBetterDiscount(OrderLine orderLine, IDiscount discount)
    {
        if (orderLine.Discount == null)
        {
            return true;
        }

        // This shouldn't really hit, we are probably checking for stackable before and
        // it's hard to see global discounts supporting stackable.
        if (discount.GlobalDiscount && IsDiscountApplicable(orderLine.OrderInfo, orderLine, discount))
        {
            return false;
        }

        if (orderLine.Discount.Type == discount.Type)
        {
            return discount.CompareTo(orderLine.Discount) > 0;
        }

        OrderedDiscount oldDiscount = orderLine.Discount;
        IPrice oldTotal = orderLine.Amount;

        orderLine.Discount = new OrderedDiscount(discount);

        bool result = orderLine.Amount.Value < oldTotal.Value;

        orderLine.Discount = oldDiscount;

        return result;
    }

    /// <summary>
    /// Although Discounts are store specific, coupons are not.
    /// We therefore 
    /// </summary>
    /// <param name="Key"></param>
    public void CouponApply(Guid Key)
    {
        IStore defStore = _storeSvc.GetAllStores().First();
        IDiscount discount = _discountCache[defStore.Alias][Key];

        (discount as Discount)?.OnCouponApply();
    }

    /// <summary>
    /// Finds Global discounts that apply to order, 
    /// checks constraints and applies automatically if applicable.
    /// </summary>
    /// <param name="orderInfo"></param>
    /// <returns></returns>
    private void AddGlobalDiscounts(OrderInfo orderInfo)
    {
        IEnumerable<IDiscount> discounts = Discounts.Instance.GetGlobalDiscounts(orderInfo.StoreInfo.Alias);

        var couponCache = Configuration.Resolver.GetService<ICouponCache>();

        foreach (IDiscount discount in discounts)
        {
            if (couponCache?.Cache.Any(x => x.Value.DiscountId == discount.Key) == true)
            {
                return;
            }

            ApplyDiscountToOrder(
                discount,
                orderInfo,
                new DiscountOrderSettings
                {
                    //UpdateOrder = false, // not technically needed for this method
                });
        }
    }

    /// <summary>
    /// Verifies all <see cref="Discount"/>'s match their constraints.
    /// Removes non-compliant <see cref="Discount"/>'s
    /// 
    /// Gets called on OrderInfo updates, constraints may become invalid if the order total changes.
    /// </summary>
    private void VerifyDiscounts(OrderInfo orderInfo)
    {
        decimal total = orderInfo.OrderLineTotal.Value;
        string storeAlias = orderInfo.StoreInfo.Alias;

        // Verify order discount constraints
        if (orderInfo.Discount?.Constraints != null
        && !orderInfo.Discount.Constraints.IsValid(
            storeAlias,
            total))
        {
            RemoveDiscountFromOrder(orderInfo);
        }

        //var curStoreDiscCache = _discountCache.GlobalDiscounts[storeAlias];

        //var gds = curStoreDiscCache
        //    .Where(gd => gd.Value.Constraints.IsValid(storeAlias, total))
        //    .Select(gd => gd.Value)
        //    .ToList();

        //// Try apply global order discounts
        //foreach (var gd in gds)
        //{
        //    //ApplyDiscountToOrder(gd, orderInfo, coupon: null);
        //}

        // Verify order line discount constraints
        foreach (OrderLine line in orderInfo.orderLines)
        {
            if (line.Discount?.Constraints != null)
            {
                if (line.Discount?.Constraints.IsValid(storeAlias, total) == false
                || !IsDiscountApplicable(orderInfo, line, line.Discount))
                {
                    RemoveDiscountFromOrderLine(line);
                }
            }
        }
    }

    /// <summary>
    /// Do constraints hold for the given discount
    /// </summary>
    /// <param name="orderInfo"></param>
    /// <param name="discount"></param>
    /// <returns></returns>
    private bool IsDiscountApplicable(IOrderInfo orderInfo, IDiscount discount)
    {
        // Constraints is set to null if there are no constraints
        if (discount.Constraints == null)
        {
            return true;
        }
        if (!discount.GlobalDiscount && !discount.DiscountItems.Any())
        {
            return false;
        }

        return discount.Constraints.IsValid(orderInfo.StoreInfo.Culture, orderInfo.OrderLineTotal.Value);
    }


    /// <summary>
    /// Do constraints hold and do discount items match if any
    /// </summary>
    /// <param name="orderInfo"></param>
    /// <param name="orderLine"></param>
    /// <param name="discount"></param>
    /// <returns></returns>
    public static bool IsDiscountApplicable(IOrderInfo orderInfo, IOrderLine orderLine, IDiscount discount)
    {
        // Constraints is set to null if there are no constraints
        if (discount.Constraints == null)
        {
            return true;
        }
        if (!discount.Stackable && orderLine.Product.ProductDiscount != null)
        {
            return false;
        }


        var constraintsOk = discount.Constraints.IsValid(
            orderInfo.StoreInfo.Culture,
            orderInfo.OrderLineTotal.Value
        );

        // Collect path items
        var pathItems = string.IsNullOrWhiteSpace(orderLine.Product.Path)
            ? Array.Empty<string>()
            : orderLine.Product.Path.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Collect category IDs (mapped via INodeService)
        var nodeSvc = Configuration.Resolver.GetService<INodeService>();
        var categoryIds = ((orderLine.Product.Properties.GetPropertyValue("categories") as string) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => nodeSvc.NodeById(x)?.Id.ToString())
            .Where(id => !string.IsNullOrEmpty(id))
            .ToArray();


        var includeSet = new HashSet<string>(discount.DiscountItems ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);
        var excludeSet = new HashSet<string>(discount.ExcludeDiscountItems ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        // Matches include rules (empty include == match all)
        bool matchesInclude =
            includeSet.Count == 0 ||
            pathItems.Any(includeSet.Contains) ||
            categoryIds.Any(includeSet.Contains);

        // Matches exclusion
        bool matchesExclude =
            excludeSet.Count > 0 &&
            (pathItems.Any(excludeSet.Contains) || categoryIds.Any(excludeSet.Contains));

        return constraintsOk && matchesInclude && !matchesExclude;
    }

    public async Task InsertCouponCodeAsync(string couponCode, int numberAvailable, Guid discountId)
    {
        if (string.IsNullOrEmpty(couponCode))
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(couponCode));
        }

        if (discountId == Guid.Empty)
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(discountId));
        }

        await _couponRepository.InsertCouponAsync(new CouponData()
        {
            CouponCode = couponCode.ToLowerInvariant(),
            CouponKey = Guid.NewGuid(),
            DiscountId = discountId,
            NumberAvailable = numberAvailable,
            Date = DateTime.Now
        }).ConfigureAwait(false);
    }

    public async Task RemoveCouponCodeAsync(string couponCode, Guid discountId)
    {
        if (string.IsNullOrEmpty(couponCode))
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(couponCode));
        }

        if (discountId == Guid.Empty)
        {
            throw new ArgumentException("== Guid.Empty", nameof(discountId));
        }

        await _couponRepository.RemoveCouponAsync(discountId, couponCode)
            .ConfigureAwait(false);
    }

    public async Task<(List<CouponData> Data, int TotalPages)> GetCouponsForDiscountAsync(Guid discountId, string query, int page, int pageSize)
    {
        if (discountId == Guid.Empty)
        {
            throw new ArgumentException("== Guid.Empty", nameof(discountId));
        }

        return await _couponRepository.GetCouponsForDiscountAsync(discountId, query, page, pageSize)
            .ConfigureAwait(false);
    }
}
