using Ekom.Events;
using Ekom.Models;
using Ekom.Models.Orders;
using Ekom.Models.Payments;
using Ekom.Repositories;
using LinqToDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace Ekom.Services;

internal sealed class OrderPaymentService : IOrderPayments
{
    private readonly DatabaseFactory _factory;
    private readonly Configuration _configuration;
    private readonly IMemoryCache _cache;
    private readonly IEnumerable<IOrderStatusPersistenceHandler> _handlers;
    private readonly ILogger<OrderPaymentService> _logger;

    public OrderPaymentService(DatabaseFactory factory, Configuration configuration, IMemoryCache cache,
        IEnumerable<IOrderStatusPersistenceHandler> handlers, ILogger<OrderPaymentService> logger)
    {
        _factory = factory;
        _configuration = configuration;
        _cache = cache;
        _handlers = handlers;
        _logger = logger;
    }

    public async Task<OrderPayment> CreateAsync(NewOrderPayment payment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (payment.OrderUniqueId == Guid.Empty)
            throw new ArgumentException("A payment must belong to an order.", nameof(payment));

        var now = DateTime.UtcNow;
        var row = new OrderPayment
        {
            PaymentId = payment.PaymentId == Guid.Empty ? Guid.NewGuid() : payment.PaymentId,
            OrderUniqueId = payment.OrderUniqueId,
            OrderNumber = payment.OrderNumber,
            AttemptId = payment.AttemptId,
            StoreAlias = payment.StoreAlias,
            ProviderKey = payment.ProviderKey,
            ProviderName = payment.ProviderName,
            Amount = payment.Amount,
            Currency = payment.Currency,
            ProviderReference = payment.ProviderReference,
            Settings = payment.Settings,
            ProviderData = payment.ProviderData,
            Member = payment.Member,
            IPAddress = payment.IPAddress,
            UserAgent = payment.UserAgent,
            CreatedUtc = now,
        };

        await using var db = _factory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        row.Sequence = Convert.ToInt32(await db.InsertWithIdentityAsync(row, token: ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
        await db.InsertAsync(new OrderPaymentTransaction
        {
            PaymentId = row.PaymentId,
            OrderUniqueId = row.OrderUniqueId,
            Type = PaymentTransactionType.Requested,
            Amount = row.Amount,
            Currency = row.Currency,
            Actor = string.IsNullOrWhiteSpace(payment.Actor) ? "Customer" : payment.Actor,
            OccurredUtc = now,
            ReceivedUtc = now,
            IdempotencyKey = $"{row.PaymentId:N}:{PaymentTransactionType.Requested}",
        }, token: ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Payment requested. OrderNumber: {OrderNumber} OrderId: {OrderUniqueId} PaymentId: {PaymentId} Provider: {PaymentProvider} Amount: {Amount} {Currency}",
            row.OrderNumber, row.OrderUniqueId, row.PaymentId, row.ProviderName, row.Amount, row.Currency);
        return row;
    }

    public async Task<PaymentRecordResult> RecordAsync(PaymentTransaction transaction, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (transaction.Type == PaymentTransactionType.Manual)
            throw new ArgumentException("Manual payment status changes are made through the order state service.", nameof(transaction));

        OrderPayment payment;
        OrderPaymentTransaction row;
        PaymentStatus? previousStatus = null;
        PaymentStatus? status = null;

        await using (var db = _factory.GetDatabase())
        {
            await using var dbTransaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            payment = await db.OrderPayments.SingleOrDefaultAsync(x => x.PaymentId == transaction.PaymentId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Payment {transaction.PaymentId} does not exist.");

            var key = string.IsNullOrWhiteSpace(transaction.IdempotencyKey)
                ? $"{payment.PaymentId:N}:{transaction.Type}:{transaction.TransactionId}"
                : transaction.IdempotencyKey;
            var existing = await db.OrderPaymentTransactions.FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct).ConfigureAwait(false);
            var order = await db.OrderData.SingleOrDefaultAsync(x => x.UniqueId == payment.OrderUniqueId, ct).ConfigureAwait(false);
            if (existing != null)
            {
                // The same provider callback delivered again: nothing new happened.
                return new PaymentRecordResult(false, order?.Status.PaymentStatus, existing);
            }

            var now = DateTime.UtcNow;
            row = new OrderPaymentTransaction
            {
                PaymentId = payment.PaymentId,
                OrderUniqueId = payment.OrderUniqueId,
                Type = transaction.Type,
                Amount = transaction.Amount ?? payment.Amount,
                Currency = payment.Currency,
                TransactionId = transaction.TransactionId,
                AuthorizationCode = transaction.AuthorizationCode,
                MaskedCardNumber = transaction.MaskedCardNumber,
                CardBrand = transaction.CardBrand,
                PaymentMethod = transaction.PaymentMethod,
                Message = transaction.Message,
                Actor = string.IsNullOrWhiteSpace(transaction.Actor) ? payment.ProviderName : transaction.Actor,
                OccurredUtc = transaction.OccurredUtc?.ToUniversalTime() ?? now,
                ReceivedUtc = now,
                IdempotencyKey = key,
                Details = transaction.Details is { Count: > 0 } ? JsonSerializer.Serialize(transaction.Details) : null,
                RawData = transaction.RawData,
            };
            row.Id = Convert.ToInt32(await db.InsertWithIdentityAsync(row, token: ct).ConfigureAwait(false), CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(transaction.TransactionId) && string.IsNullOrWhiteSpace(payment.TransactionId))
            {
                payment.TransactionId = transaction.TransactionId;
                await db.OrderPayments.Where(x => x.PaymentId == payment.PaymentId)
                    .Set(x => x.TransactionId, transaction.TransactionId).UpdateAsync(ct).ConfigureAwait(false);
            }

            if (order != null)
            {
                previousStatus = order.Status.PaymentStatus;
                status = await ApplyStatusAsync(db, order, ct).ConfigureAwait(false);
                await db.InsertAsync(new OrderActivityLog
                {
                    UniqueID = Guid.NewGuid(),
                    Key = order.UniqueId,
                    Date = DateTime.Now,
                    UserName = row.Actor ?? string.Empty,
                    LogType = LogType(row.Type),
                    Log = ActivityMessage(payment, row),
                }, token: ct).ConfigureAwait(false);
            }

            await dbTransaction.CommitAsync(ct).ConfigureAwait(false);
        }

        _cache.Remove(payment.OrderUniqueId);
        _cache.Remove(payment.OrderUniqueId.ToString());

        _logger.Log(row.Type is PaymentTransactionType.Failed ? LogLevel.Warning : LogLevel.Information,
            "Payment {PaymentEvent}. OrderNumber: {OrderNumber} OrderId: {OrderUniqueId} PaymentId: {PaymentId} Provider: {PaymentProvider} Amount: {Amount} {Currency} TransactionId: {TransactionId} PaymentStatus: {PaymentStatus}",
            row.Type, payment.OrderNumber, payment.OrderUniqueId, payment.PaymentId, payment.ProviderName, row.Amount, row.Currency,
            row.TransactionId, status);

        await OrderEvents.OnPaymentRecordedAsync(this, new PaymentRecordedEventArgs
        {
            Payment = payment,
            Transaction = row,
            PreviousPaymentStatus = previousStatus,
            PaymentStatus = status,
        }, ct).ConfigureAwait(false);

        return new PaymentRecordResult(true, status, row);
    }

    /// <summary>
    /// Writes the calculated payment status to the order. Orders that have not been moved to the
    /// independent statuses yet are left alone: their status still comes from the legacy record.
    /// </summary>
    private async Task<PaymentStatus?> ApplyStatusAsync(DbContext db, OrderData order, CancellationToken ct)
    {
        var current = order.Status;
        if (order.StatusMappingVersion == 0 || current.RequiresReview) return current.PaymentStatus;

        var rows = await db.OrderPaymentTransactions.Where(x => x.OrderUniqueId == order.UniqueId).ToListAsync(ct).ConfigureAwait(false);
        var calculated = PaymentStatusCalculator.Calculate(rows, order.TotalAmount);
        if (calculated == null || calculated == current.PaymentStatus) return current.PaymentStatus;

        var previousLegacy = order.OrderStatusCol;
        order.PaymentStateCol = calculated.ToString();
        OrderStateRules.AutoClose(order, _configuration.AutoClosePaidAndFulfilledOrders(order.StoreAlias));
        order.OrderStatusCol = OrderStateRules.Project(order);
        order.StatusRevision = checked(order.StatusRevision + 1);
        order.UpdateDate = DateTime.Now;
        await OrderStateService.WriteStateAsync(db, order, ct).ConfigureAwait(false);
        if (previousLegacy != order.OrderStatusCol)
        {
            foreach (var handler in _handlers)
                await handler.PersistAsync(db, order, previousLegacy, ct).ConfigureAwait(false);
        }
        return calculated;
    }

    internal static OrderPaymentTransaction ManualTransaction(Guid orderId, PaymentStatus status, string? userName, string? currency = null)
    {
        var now = DateTime.UtcNow;
        return new OrderPaymentTransaction
        {
            OrderUniqueId = orderId,
            Type = PaymentTransactionType.Manual,
            ManualStatusCol = status.ToString(),
            Currency = currency,
            Actor = userName,
            Message = $"Payment status set to {status}",
            OccurredUtc = now,
            ReceivedUtc = now,
            IdempotencyKey = $"manual:{Guid.NewGuid():N}",
        };
    }

    private static OrderActivityLogType LogType(PaymentTransactionType type) => type switch
    {
        PaymentTransactionType.Captured or PaymentTransactionType.Authorized => OrderActivityLogType.Success,
        PaymentTransactionType.Failed => OrderActivityLogType.Alert,
        _ => OrderActivityLogType.Info,
    };

    private static string ActivityMessage(OrderPayment payment, OrderPaymentTransaction row)
    {
        var outcome = row.Type switch
        {
            PaymentTransactionType.Captured => "Payment successful",
            PaymentTransactionType.Authorized => "Payment authorized",
            PaymentTransactionType.Failed => "Payment failed",
            PaymentTransactionType.Cancelled => "Payment cancelled",
            PaymentTransactionType.Voided => "Payment voided",
            PaymentTransactionType.Refunded => "Payment refunded",
            _ => "Payment requested",
        };
        var message = string.Create(CultureInfo.InvariantCulture,
            $"{outcome}. Order number: {payment.OrderNumber}; Amount: {row.Amount} {row.Currency}; Payment provider: {payment.ProviderName}");
        if (!string.IsNullOrWhiteSpace(row.TransactionId)) message += $"; Transaction ID: {row.TransactionId}";
        if (!string.IsNullOrWhiteSpace(row.Message)) message += $"; {row.Message}";
        return message + $"; PaymentId: {payment.PaymentId}.";
    }

    public async Task<OrderPayment?> GetAsync(Guid paymentId, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        return await db.OrderPayments.SingleOrDefaultAsync(x => x.PaymentId == paymentId, ct).ConfigureAwait(false);
    }

    public async Task<OrderPayment?> GetByProviderReferenceAsync(string providerName, string providerReference, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerReference)) return null;
        await using var db = _factory.GetDatabase();
        return await db.OrderPayments
            .Where(x => x.ProviderReference == providerReference && x.ProviderName == providerName)
            .OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<Guid?> FindOrderAsync(string paymentIdOrTransactionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paymentIdOrTransactionId)) return null;
        var value = paymentIdOrTransactionId.Trim();
        await using var db = _factory.GetDatabase();
        if (Guid.TryParse(value, out var paymentId))
        {
            var byId = await db.OrderPayments.Where(x => x.PaymentId == paymentId)
                .Select(x => (Guid?)x.OrderUniqueId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (byId != null) return byId;
        }
        return await db.OrderPaymentTransactions.Where(x => x.TransactionId == value)
            .Select(x => (Guid?)x.OrderUniqueId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<OrderPaymentsView> GetForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        var payments = await db.OrderPayments.Where(x => x.OrderUniqueId == orderId).ToListAsync(ct).ConfigureAwait(false);
        var rows = await db.OrderPaymentTransactions.Where(x => x.OrderUniqueId == orderId).ToListAsync(ct).ConfigureAwait(false);
        return BuildView(payments, rows);
    }

    internal static OrderPaymentsView BuildView(IReadOnlyCollection<OrderPayment> payments, IReadOnlyCollection<OrderPaymentTransaction> rows)
    {
        var attempts = payments.OrderByDescending(x => x.Sequence).Select(payment =>
        {
            var own = rows.Where(x => x.PaymentId == payment.PaymentId).OrderBy(x => x.OccurredUtc).ThenBy(x => x.Id).ToList();
            var captured = own.FirstOrDefault(x => x.Type == PaymentTransactionType.Captured);
            var authorized = own.FirstOrDefault(x => x.Type == PaymentTransactionType.Authorized);
            // The newest row that carries the value wins, so a capture's details replace an authorization's.
            string? Latest(Func<OrderPaymentTransaction, string?> pick)
                => own.Select(pick).LastOrDefault(x => !string.IsNullOrWhiteSpace(x));
            return new OrderPaymentAttempt
            {
                PaymentId = payment.PaymentId,
                AttemptId = payment.AttemptId,
                Provider = payment.ProviderName,
                Status = PaymentStatusCalculator.DescribeAttempt(own),
                Amount = payment.Amount,
                Currency = payment.Currency,
                TransactionId = payment.TransactionId ?? Latest(x => x.TransactionId),
                CreatedUtc = payment.CreatedUtc,
                AuthorizedUtc = authorized?.OccurredUtc,
                CapturedUtc = captured?.OccurredUtc,
                AuthorizationCode = Latest(x => x.AuthorizationCode),
                MaskedCardNumber = Latest(x => x.MaskedCardNumber),
                CardBrand = Latest(x => x.CardBrand),
                PaymentMethod = Latest(x => x.PaymentMethod),
                Message = own.LastOrDefault(x => x.Type is PaymentTransactionType.Failed or PaymentTransactionType.Cancelled)?.Message,
                Details = own.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.Details))?.GetDetails() ?? Array.Empty<PaymentDetail>(),
                Events = own.Select(ToEvent).ToList(),
            };
        }).ToList();

        var current = attempts.FirstOrDefault(x => x.CapturedUtc != null)
            ?? attempts.FirstOrDefault(x => x.Status == nameof(PaymentStatus.Authorized))
            ?? attempts.FirstOrDefault();
        var manual = rows.Where(x => x.Type == PaymentTransactionType.Manual)
            .OrderByDescending(x => x.OccurredUtc).ThenByDescending(x => x.Id).Select(ToEvent).ToList();
        return new OrderPaymentsView(current, attempts, manual);
    }

    private static OrderPaymentEvent ToEvent(OrderPaymentTransaction row)
        => new(row.Type.ToString(), row.Amount, row.Currency, row.OccurredUtc, row.Message, row.Actor, row.TransactionId, row.ManualStatusCol);
}
