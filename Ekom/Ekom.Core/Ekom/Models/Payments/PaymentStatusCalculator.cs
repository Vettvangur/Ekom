using Ekom.Models.Orders;

namespace Ekom.Models.Payments;

/// <summary>
/// Calculates an order's payment status from its payment transactions.
/// The transactions are the source of truth; the status stored on the order is this result.
/// </summary>
public static class PaymentStatusCalculator
{
    /// <param name="transactions">All payment transactions of one order, in any order.</param>
    /// <param name="orderTotal">The amount the order must be paid with.</param>
    /// <returns>Null when there is nothing to calculate from.</returns>
    public static PaymentStatus? Calculate(IEnumerable<OrderPaymentTransaction> transactions, decimal orderTotal)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        var rows = transactions.OrderBy(x => x.OccurredUtc).ThenBy(x => x.Id).ToList();
        if (rows.Count == 0) return null;

        var manualIndex = rows.FindLastIndex(x => x.Type == PaymentTransactionType.Manual && x.ManualStatus.HasValue);
        if (manualIndex >= 0)
        {
            // A manual choice overrides everything before it. After it, only real money
            // movements change the status: a late failure of an old attempt must not undo it.
            var moneyMovedAfter = rows.Skip(manualIndex + 1).Any(x => x.Type
                is PaymentTransactionType.Captured or PaymentTransactionType.Refunded or PaymentTransactionType.Voided);
            if (!moneyMovedAfter) return rows[manualIndex].ManualStatus;
            rows.RemoveAll(x => x.Type == PaymentTransactionType.Manual);
        }

        var captured = rows.Where(x => x.Type == PaymentTransactionType.Captured).ToList();
        if (captured.Count > 0)
        {
            var capturedAmount = captured.Sum(x => x.Amount);
            var refundedAmount = rows.Where(x => x.Type == PaymentTransactionType.Refunded).Sum(x => x.Amount);
            if (refundedAmount > 0)
                return refundedAmount >= capturedAmount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
            return capturedAmount >= orderTotal ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;
        }

        // An authorization holds until the same payment is voided.
        var authorized = rows.Any(x => x.Type == PaymentTransactionType.Authorized
            && !rows.Any(v => v.Type == PaymentTransactionType.Voided && v.PaymentId == x.PaymentId
                && (v.OccurredUtc > x.OccurredUtc || (v.OccurredUtc == x.OccurredUtc && v.Id > x.Id))));
        if (authorized) return PaymentStatus.Authorized;

        return rows[^1].Type switch
        {
            PaymentTransactionType.Failed => PaymentStatus.Failed,
            PaymentTransactionType.Voided => PaymentStatus.Voided,
            // Requested, or cancelled by the customer: the order is still waiting for a payment.
            _ => PaymentStatus.Pending,
        };
    }

    /// <summary>The state of one payment attempt, for display.</summary>
    public static string DescribeAttempt(IEnumerable<OrderPaymentTransaction> paymentTransactions)
    {
        ArgumentNullException.ThrowIfNull(paymentTransactions);
        var rows = paymentTransactions.OrderBy(x => x.OccurredUtc).ThenBy(x => x.Id).ToList();
        var captured = rows.Where(x => x.Type == PaymentTransactionType.Captured).Sum(x => x.Amount);
        var refunded = rows.Where(x => x.Type == PaymentTransactionType.Refunded).Sum(x => x.Amount);
        if (rows.Any(x => x.Type == PaymentTransactionType.Captured))
        {
            if (refunded > 0) return refunded >= captured ? nameof(PaymentStatus.Refunded) : nameof(PaymentStatus.PartiallyRefunded);
            return nameof(PaymentStatus.Paid);
        }
        var last = rows.LastOrDefault(x => x.Type != PaymentTransactionType.Requested);
        return last?.Type switch
        {
            PaymentTransactionType.Authorized => nameof(PaymentStatus.Authorized),
            PaymentTransactionType.Failed => nameof(PaymentStatus.Failed),
            PaymentTransactionType.Voided => nameof(PaymentStatus.Voided),
            PaymentTransactionType.Cancelled => nameof(PaymentTransactionType.Cancelled),
            _ => nameof(PaymentStatus.Pending),
        };
    }
}
