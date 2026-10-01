using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Ekom.Services;

internal sealed class PaymentSuccessLogService
{
    private readonly ActivityLogRepository _repository;
    private readonly ILogger<PaymentSuccessLogService> _logger;

    public PaymentSuccessLogService(ActivityLogRepository repository, ILogger<PaymentSuccessLogService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task LogAsync(Guid orderId, string orderNumber, decimal amount, string currency,
        string paymentProvider, bool offlinePayment = false)
    {
        string outcome = offlinePayment ? "Offline Payment Successfull" : "Payment successful";

        _logger.LogInformation(
            "{PaymentOutcome}. Amount: {Amount} {Currency}; Payment provider: {PaymentProvider}; Order number: {OrderNumber}; UniqueId: {OrderUniqueId}.",
            outcome, amount, currency, paymentProvider, orderNumber, orderId);

        string message = string.Create(CultureInfo.InvariantCulture,
            $"{outcome}. Amount: {amount} {currency}; Payment provider: {paymentProvider}; Order number: {orderNumber}; UniqueId: {orderId}.");

        try
        {
            // Persist before checkout completion, independently of its work and the activity-log queue.
            await _repository.InsertAsync(
                [new OrderActivityLogWrite(orderId, message, "Customer", DateTime.Now, OrderActivityLogType.Success)],
                CancellationToken.None).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Logging failures must not block completion of an already-paid order.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // A log-write failure must not prevent an already-paid order from completing.
            _logger.LogError(ex,
                "Failed to persist payment-success activity log. Order number: {OrderNumber}; UniqueId: {OrderUniqueId}.",
                orderNumber, orderId);
        }
    }
}
