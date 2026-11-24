using System.Net;
using System.Text.Json.Serialization;

namespace Ekom.Models
{
    /// <summary>A customer-safe checkout conflict; never contains diagnostic exception text.</summary>
    public sealed class CheckoutStateError
    {
        [JsonPropertyName("code")]
        [Newtonsoft.Json.JsonProperty("code")]
        public string Code { get; }

        [JsonPropertyName("message")]
        [Newtonsoft.Json.JsonProperty("message")]
        public string Message { get; }

        [JsonPropertyName("canRetry")]
        [Newtonsoft.Json.JsonProperty("canRetry")]
        public bool CanRetry { get; }

        /// <summary>The order line the customer must remove. Only set for <c>productUnavailable</c>.</summary>
        [JsonPropertyName("orderLineKey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Newtonsoft.Json.JsonProperty("orderLineKey", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Guid? OrderLineKey { get; private init; }

        /// <summary>SKU of the unavailable product or variant. Only set for <c>productUnavailable</c>.</summary>
        [JsonPropertyName("sku")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Newtonsoft.Json.JsonProperty("sku", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string? Sku { get; private init; }

        /// <summary>A product or variant on an order line no longer exists in the store.</summary>
        public static CheckoutStateError ProductUnavailable(Guid orderLineKey, string? sku, string? title)
        {
            sku = string.IsNullOrWhiteSpace(sku) ? null : sku;
            var name = !string.IsNullOrWhiteSpace(title) ? $"\"{title}\"" : sku != null ? $"The product with SKU {sku}" : null;
            return new CheckoutStateError("productUnavailable", name == null
                ? UnavailableProductMessage
                : $"{name} is no longer available. Please remove it from your basket to continue.")
            {
                OrderLineKey = orderLineKey == Guid.Empty ? null : orderLineKey,
                Sku = sku,
            };
        }

        private const string UnavailableProductMessage = "A product in your basket is no longer available. Please remove it to continue.";

        private CheckoutStateError(string code, string message)
        {
            Code = code;
            Message = message;
            CanRetry = false;
        }

        public CheckoutStateError(CheckoutConflictReason reason = CheckoutConflictReason.PreparationUnavailable)
        {
            (Code, Message) = reason switch
            {
                CheckoutConflictReason.Busy => ("checkoutBusy", "Checkout is already in progress. Please wait a moment and try again."),
                CheckoutConflictReason.PaymentReview => ("paymentReviewRequired", "Your payment needs to be reviewed before checkout can continue. Please contact the store before making another payment."),
                CheckoutConflictReason.CouponUnavailable => ("couponUnavailable", "The coupon on this basket can no longer be used. Please remove it and try again."),
                CheckoutConflictReason.ProductUnavailable => ("productUnavailable", UnavailableProductMessage),
                CheckoutConflictReason.Completed => ("checkoutCompleted", "This checkout has already been completed. Please check your order confirmation before making another payment."),
                _ => ("checkout_state_conflict", "Unable to prepare checkout. Please refresh your basket and try again. If the problem continues, contact the store."),
            };
            // A conflict is not permission to automatically resubmit a payment.
            CanRetry = false;
        }
    }

    public enum CheckoutConflictReason
    {
        PreparationUnavailable,
        Busy,
        PaymentReview,
        Completed,
        CouponUnavailable,
        ProductUnavailable,
    }
}

namespace Ekom.Exceptions
{
    /// <summary>A checkout conflict with an explicit customer-safe classification.</summary>
    public sealed class CheckoutConflictException : EkomHttpException
    {
        public Ekom.Models.CheckoutConflictReason Reason { get; }

        public CheckoutConflictException(Ekom.Models.CheckoutConflictReason reason, string message)
            : base(HttpStatusCode.Conflict, message)
        {
            Reason = reason;
        }
    }
}
