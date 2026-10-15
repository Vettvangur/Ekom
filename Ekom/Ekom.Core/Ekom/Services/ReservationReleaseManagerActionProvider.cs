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
        var actions = new List<OrderManagerAction>();
        var cards = await attempts.GetManagerGiftcardReleaseStatesAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
        foreach (var card in cards)
            actions.Add(new OrderManagerAction
            {
                Key = GiftcardsPrefix + card.Identifier + ":" + card.Token,
                Label = "Release gift card",
                GiftcardCode = card.Code,
                Enabled = card.DisabledReason == null,
                DisabledReason = card.DisabledReason,
                ConfirmMessage = "Release only this gift card's unredeemed reservation with the provider? Stock reservations, other gift-card reservations, and all selected gift cards will be kept. This is not a refund.",
                SortOrder = 900,
            });
        var state = await attempts.GetManagerReleaseStateAsync(orderInfo.UniqueId, ct).ConfigureAwait(false);
        if (state == null || !state.HasCheckoutReservations)
            return actions;
        actions.Add(new OrderManagerAction
        {
            Key = CheckoutPrefix + state.Token,
            Label = "Release checkout reservations",
            Enabled = state.CanRelease,
            DisabledReason = state.CanRelease ? null : "The gift-card reservation integration is not configured.",
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

        var parts = actionKey[(giftcardsOnly ? GiftcardsPrefix.Length : CheckoutPrefix.Length)..].Split(':');
        if (parts.Length != (giftcardsOnly ? 2 : 1) || parts.Any(part => part.Length != 64 || !part.All(Uri.IsHexDigit)))
            return new OrderManagerActionBadRequestResult { Message = "Invalid reservation release action. Refresh the order." };
        try
        {
            var released = giftcardsOnly
                ? await attempts.ReleaseGiftcardForManagerAsync(orderInfo.UniqueId, orderInfo.StoreInfo.Alias,
                    parts[0], parts[1], userName, ct).ConfigureAwait(false)
                : await attempts.ReleaseForManagerAsync(orderInfo.UniqueId, orderInfo.StoreInfo.Alias,
                    parts[0], false, userName, ct).ConfigureAwait(false);
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
