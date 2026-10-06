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

        public CheckoutStateError(CheckoutConflictReason reason = CheckoutConflictReason.PreparationUnavailable)
        {
            (Code, Message) = reason switch
            {
                CheckoutConflictReason.Busy => ("checkoutBusy", "Checkout is already in progress. Please wait for it to finish or cancel the existing payment before trying again."),
                CheckoutConflictReason.PaymentReview => ("paymentReviewRequired", "Your payment needs to be reviewed before checkout can continue. Please contact the store before making another payment."),
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
