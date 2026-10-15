using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Models.Manager;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

internal sealed class ReservationReleaseManagerActionProvider(
    CheckoutPaymentAttemptService attempts,
    ILogger<ReservationReleaseManagerActionProvider> logger) : IOrderManagerActionProvider
{
    private const string GiftcardsPrefix = "ekom.release-giftcards:";
    private const string CheckoutPrefix = "ekom.release-checkout:";

    public async Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(IOrderInfo orderInfo, CancellationToken ct = default)
    {
        var state = await attempts.GetManagerReleaseStateAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
        if (state == null || !state.HasCheckoutReservations)
            return Array.Empty<OrderManagerAction>();

        var actions = new List<OrderManagerAction>();
        if (state.HasGiftcardClaims)
            actions.Add(new OrderManagerAction
            {
                Key = GiftcardsPrefix + state.Token,
                Label = "Release gift-card reservations",
                Enabled = state.CanRelease,
                ConfirmMessage = state.HasActiveAttempt
                    ? "This gift card belongs to a payment attempt. Release the entire unpaid payment attempt, including all gift-card and stock reservations? Do not proceed if payment may already have been collected. This does not cancel a payment at the payment provider or issue a refund."
                    : "Release this order's unredeemed gift-card reservations with the provider? Stock reservations and the selected gift cards will be kept. This is not a refund.",
                SortOrder = 900,
            });
        actions.Add(new OrderManagerAction
        {
            Key = CheckoutPrefix + state.Token,
            Label = "Release checkout reservations",
            Enabled = state.CanRelease,
            ConfirmMessage = "Release all stock and unredeemed gift-card reservations and close the current unpaid payment attempt? The order will return to Incomplete. Do not proceed if payment may already have been collected. This does not cancel a payment at the payment provider or issue a refund.",
            SortOrder = 910,
        });
        return actions;
    }

    public async Task<OrderManagerActionExecutionResult?> ExecuteAsync(IOrderInfo orderInfo, string actionKey,
        string? userName = null, CancellationToken ct = default)
    {
        var giftcardsOnly = actionKey.StartsWith(GiftcardsPrefix, StringComparison.Ordinal);
        if (!giftcardsOnly && !actionKey.StartsWith(CheckoutPrefix, StringComparison.Ordinal))
            return null;

        var token = actionKey[(giftcardsOnly ? GiftcardsPrefix.Length : CheckoutPrefix.Length)..];
        if (token.Length != 64 || !token.All(Uri.IsHexDigit))
            return new OrderManagerActionBadRequestResult { Message = "Invalid reservation release action. Refresh the order." };
        try
        {
            var released = await attempts.ReleaseForManagerAsync(orderInfo.UniqueId, orderInfo.StoreInfo.Alias,
                token, giftcardsOnly, userName, ct).ConfigureAwait(false);
            return new OrderManagerActionSuccessResult
            {
                Message = released ? "Reservations released successfully." : "These reservations have already been released.",
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (EkomHttpException ex)
        {
            return new OrderManagerActionBadRequestResult { Message = ex.Message };
        }
        catch (Exception ex)
        {
            logger.LogError("Manual reservation release failed for order {OrderId}. FailureType: {FailureType}",
                orderInfo.UniqueId, ex.GetType().Name);
            return new OrderManagerActionBadRequestResult
            {
                Message = "Reservation release could not be confirmed. Check the activity log and provider before retrying; reservations may remain held.",
            };
        }
    }
}
