using Ekom.Models;

namespace Ekom.Events;

public class DiscountEvents
{
    public event Func<object?, ProductDiscountEvaluationEventArgs, Task>? BeforeEvaluateDiscountsAsync;
    public event Func<object?, ProductDiscountApplicableEventArgs, Task>? AfterApplicableDiscountsAsync;

    /// <summary>
    /// Runs before whole-order coupon validation, normalization, store resolution, or lookup.
    /// Call <see cref="BeforeApplyCouponDiscountEventArgs.Reject"/> to prevent application.
    /// </summary>
    public event Func<object?, BeforeApplyCouponDiscountEventArgs, Task>? BeforeApplyCouponDiscountAsync;

    public async Task RaiseBeforeApplyCouponDiscountAsync(object sender, BeforeApplyCouponDiscountEventArgs e, CancellationToken ct)
    {
        var handlers = BeforeApplyCouponDiscountAsync;
        ct.ThrowIfCancellationRequested();
        if (handlers == null || e.IsRejected) return;

        foreach (var handler in handlers.GetInvocationList()
            .Cast<Func<object?, BeforeApplyCouponDiscountEventArgs, Task>>())
        {
            ct.ThrowIfCancellationRequested();
            await handler(sender, e).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (e.IsRejected) return;
        }
    }

    public sealed class BeforeApplyCouponDiscountEventArgs : EventArgs
    {
        /// <summary>
        /// Raw submitted code, before normalization or validation. It may be null or empty.
        /// </summary>
        public string? CouponCode { get; }

        /// <summary>
        /// Raw supplied alias; null when the caller uses the current-store overload.
        /// The current store has not been resolved yet.
        /// </summary>
        public string? StoreAlias { get; }

        public CancellationToken CancellationToken { get; }
        public bool IsRejected => RejectionReason != null;
        public string? RejectionReason { get; private set; }

        public BeforeApplyCouponDiscountEventArgs(string? couponCode, string? storeAlias, CancellationToken cancellationToken)
        {
            CouponCode = couponCode;
            StoreAlias = storeAlias;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Reject this application with a customer-facing reason. The first rejection is preserved.
        /// </summary>
        public void Reject(string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            RejectionReason ??= reason;
        }
    }

    public async Task RaiseBeforeEvaluateDiscountsAsync(object sender, ProductDiscountEvaluationEventArgs e, CancellationToken ct)
    {
        if (BeforeEvaluateDiscountsAsync == null)
            return;

        foreach (var handler in BeforeEvaluateDiscountsAsync
            .GetInvocationList()
            .Cast<Func<object?, ProductDiscountEvaluationEventArgs, Task>>())
        {
            ct.ThrowIfCancellationRequested();
            await handler(sender, e).ConfigureAwait(false);
        }
    }

    public async Task RaiseAfterApplicableDiscountsAsync(object sender, ProductDiscountApplicableEventArgs e, CancellationToken ct)
    {
        if (AfterApplicableDiscountsAsync == null)
            return;

        foreach (var handler in AfterApplicableDiscountsAsync
            .GetInvocationList()
            .Cast<Func<object?, ProductDiscountApplicableEventArgs, Task>>())
        {
            ct.ThrowIfCancellationRequested();
            await handler(sender, e).ConfigureAwait(false);
        }
    }

    public class ProductDiscountEvaluationEventArgs : EventArgs
    {
        public string Path { get; set; } = string.Empty;
        public string StoreAlias { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string[]? Categories { get; set; }

        /// <summary>
        /// Ambient <see cref="Ekom.PricingContext"/> active for this evaluation (e.g. per order line during
        /// order discount calculation). Case-insensitive, empty when no context is active.
        /// </summary>
        public IReadOnlyDictionary<string, string> PricingContext { get; init; } = Ekom.PricingContext.CurrentOrEmpty;
    }

    public class ProductDiscountApplicableEventArgs : EventArgs
    {
        public string Path { get; }
        public string StoreAlias { get; }
        public decimal Price { get; }
        public string[]? Categories { get; }
        public List<IProductDiscount> ApplicableDiscounts { get; }

        /// <inheritdoc cref="ProductDiscountEvaluationEventArgs.PricingContext"/>
        public IReadOnlyDictionary<string, string> PricingContext { get; }

        public ProductDiscountApplicableEventArgs(
            string path,
            string storeAlias,
            decimal price,
            string[]? categories,
            List<IProductDiscount> applicableDiscounts)
            : this(path, storeAlias, price, categories, applicableDiscounts, Ekom.PricingContext.CurrentOrEmpty)
        {
        }

        public ProductDiscountApplicableEventArgs(
            string path,
            string storeAlias,
            decimal price,
            string[]? categories,
            List<IProductDiscount> applicableDiscounts,
            IReadOnlyDictionary<string, string> pricingContext)
        {
            Path = path;
            StoreAlias = storeAlias;
            Price = price;
            Categories = categories;
            ApplicableDiscounts = applicableDiscounts;
            PricingContext = pricingContext;
        }
    }

}
