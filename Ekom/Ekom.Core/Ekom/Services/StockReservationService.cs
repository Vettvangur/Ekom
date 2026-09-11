using Ekom.Models;
using Ekom.Repositories;
using LinqToDB;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ekom.Services;

internal sealed class StockReservationService : IStockReservationService
{
    internal static readonly Meter Meter = new("Ekom.Reservations");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("ekom.reservations.outcomes");
    private static readonly Counter<long> ExpiryFailures = Meter.CreateCounter<long>("ekom.reservations.expiry.failures");
    private readonly DatabaseFactory _database;
    private readonly Configuration _config;
    private readonly StockChangePublisher _publisher;
    private readonly ILogger<StockReservationService> _logger;

    public StockReservationService(DatabaseFactory database, Configuration config,
        StockChangePublisher publisher, ILogger<StockReservationService> logger)
    {
        _database = database;
        _config = config;
        _publisher = publisher;
        _logger = logger;
    }

    private static void ValidateRequest(StockReservationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Key == Guid.Empty) throw new ArgumentException("A stock key is required.", nameof(request));
        if (request.Quantity <= 0 || request.Quantity > 9999999999999999.99m || decimal.Round(request.Quantity, 2) != request.Quantity)
            throw new ArgumentOutOfRangeException(nameof(request), "Quantity must be positive with at most two decimal places.");
        if (request.IsDiscount && (request.Quantity != decimal.Truncate(request.Quantity) || request.Quantity > int.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(request), "Discount quantity must be a positive integer.");
        if (request.MinimumRemainingStock < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Minimum remaining stock cannot be negative.");
        if (!request.IsDiscount && request.Coupon != null)
            throw new ArgumentException("Coupons apply only to discount reservations.", nameof(request));
        if (request.StoreAlias?.Length > 255 || request.Coupon?.Length > 255 ||
            request.OrderId?.Length > 255 || request.PaymentAttemptId?.Length > 255)
            throw new ArgumentException("Reservation identifiers cannot exceed 255 characters.", nameof(request));
        if (request.IdempotencyKey != null && string.IsNullOrWhiteSpace(request.IdempotencyKey))
            throw new ArgumentException("Idempotency key cannot be blank.", nameof(request));
    }

    private StockReservationData CreateReservation(StockReservationRequest request)
    {
        ValidateRequest(request);
        if (!request.IsDiscount && _config.PerStoreStock && string.IsNullOrWhiteSpace(request.StoreAlias))
            throw new ArgumentException("Per-store reservations require an explicit store alias.", nameof(request));
        var duration = request.Duration == default ? _config.ReservationTimeout : request.Duration;
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(request), "Duration must be positive.");
        var stockStore = !request.IsDiscount && _config.PerStoreStock ? request.StoreAlias : null;
        var stockId = request.IsDiscount
            ? request.Coupon == null ? request.Key.ToString() : $"{request.Key}_{request.Coupon}"
            : stockStore == null ? request.Key.ToString() : $"{stockStore}_{request.Key}";
        if (stockId.Length > 255)
            throw new ArgumentException("Reservation identifiers cannot exceed 255 characters.", nameof(request));

        var now = DateTime.UtcNow;
        return new StockReservationData
        {
            Id = Guid.NewGuid().ToString(),
            CreationKey = request.IdempotencyKey == null ? "g:" + Guid.NewGuid().ToString("N") : "k:" + Hash(request.IdempotencyKey),
            PayloadHash = Hash(JsonSerializer.Serialize(new
            {
                request.Key, Quantity = request.Quantity.ToString("G29", CultureInfo.InvariantCulture), request.IsDiscount, request.StoreAlias, request.Coupon,
                request.OrderId, request.PaymentAttemptId, request.MinimumRemainingStock, DurationTicks = duration.Ticks, StockId = stockId,
            })),
            Key = request.Key, StockUniqueId = stockId, IsDiscount = request.IsDiscount,
            StoreAlias = request.StoreAlias, Coupon = request.Coupon, Quantity = request.Quantity,
            OrderId = request.OrderId, PaymentAttemptId = request.PaymentAttemptId,
            CreatedUtc = now, ExpiresUtc = now.Add(duration), State = StockReservationState.Active,
        };
    }

    public async Task<StockReservationResult> ReserveAsync(StockReservationRequest request, CancellationToken ct = default)
    {
        var row = CreateReservation(request);
        var stockId = row.StockUniqueId;
        var now = row.CreatedUtc;
        decimal oldStock = 0;
        var result = await RetryAsync(async () =>
        {
            await using var db = _database.GetDatabase();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            var existing = await db.StockReservations.FirstOrDefaultAsync(x => x.CreationKey == row.CreationKey, ct).ConfigureAwait(false);
            if (existing != null)
                return new StockReservationResult(existing.Id, existing.PayloadHash == row.PayloadHash
                    ? StockReservationStatus.AlreadyExists : StockReservationStatus.Conflict, existing.State, existing.ExpiresUtc);

            int affected;
            if (row.IsDiscount)
            {
                var quantity = (int)row.Quantity;
                affected = await db.DiscountStockData.Where(x => x.UniqueId == stockId && x.Stock >= quantity)
                    .Set(x => x.Stock, x => x.Stock - quantity).Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
            }
            else
            {
                oldStock = await db.StockData.Where(x => x.UniqueId == stockId).Select(x => x.Stock).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                // SQLite arithmetic uses floating point; retain the stock table's two-decimal precision.
                affected = await db.StockData.Where(x => x.UniqueId == stockId && x.Stock >= row.Quantity + request.MinimumRemainingStock)
                    .Set(x => x.Stock, x => Math.Round(x.Stock - row.Quantity, 2)).Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
            }
            if (affected == 0) return new StockReservationResult(null, StockReservationStatus.InsufficientStock);
            await db.InsertAsync(row, token: ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return new StockReservationResult(row.Id, StockReservationStatus.Created, row.State, row.ExpiresUtc);
        }, ct).ConfigureAwait(false);

        if (result.Status == StockReservationStatus.Created && !row.IsDiscount)
        {
            var stockStore = stockId == row.Key.ToString() ? null : row.StoreAlias;
            await _publisher.PublishAsync(row.Key, stockStore, stockId, oldStock, decimal.Round(oldStock - row.Quantity, 2), ct).ConfigureAwait(false);
        }
        return Record(result);
    }

    public Task<StockReservationResult> ConsumeAsync(string reservationId, CancellationToken ct = default)
        => TransitionAsync(reservationId, StockReservationState.Consumed, ct);
    public Task<StockReservationResult> ReleaseAsync(string reservationId, CancellationToken ct = default)
        => TransitionAsync(reservationId, StockReservationState.Released, ct);
    public Task<StockReservationResult> ExpireAsync(string reservationId, CancellationToken ct = default)
        => TransitionAsync(reservationId, StockReservationState.Expired, ct);

    private async Task<StockReservationResult> TransitionAsync(string id, StockReservationState target, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        StockReservationData? restored = null;
        decimal oldStock = 0;
        var result = await RetryAsync(async () =>
        {
            restored = null;
            await using var db = _database.GetDatabase();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            var row = await db.StockReservations.FirstOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
            if (row == null) return new StockReservationResult(id, StockReservationStatus.NotFound);
            if (row.State != StockReservationState.Active)
                return new StockReservationResult(id, row.State switch
                {
                    StockReservationState.Consumed => StockReservationStatus.AlreadyConsumed,
                    StockReservationState.Released => StockReservationStatus.AlreadyReleased,
                    _ => target == StockReservationState.Consumed ? StockReservationStatus.LateConsumption : StockReservationStatus.AlreadyExpired,
                }, row.State, row.ExpiresUtc);
            var now = DateTime.UtcNow;
            if (target == StockReservationState.Expired && row.ExpiresUtc > now)
                return new StockReservationResult(id, StockReservationStatus.NotDue, row.State, row.ExpiresUtc);
            var late = target == StockReservationState.Consumed && row.ExpiresUtc <= now;
            var state = late ? StockReservationState.Expired : target;
            // The conditional write, not a process-local lock, owns restoration.
            var affected = await db.StockReservations.Where(x => x.Id == id && x.State == StockReservationState.Active)
                .Set(x => x.State, state).Set(x => x.CompletedUtc, now).UpdateAsync(ct).ConfigureAwait(false);
            if (affected != 1) throw new DBConcurrencyException("Reservation changed concurrently.");
            if (state != StockReservationState.Consumed)
            {
                if (row.IsDiscount)
                {
                    var quantity = (int)row.Quantity;
                    affected = await db.DiscountStockData.Where(x => x.UniqueId == row.StockUniqueId)
                        .Set(x => x.Stock, x => x.Stock + quantity).Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    oldStock = await db.StockData.Where(x => x.UniqueId == row.StockUniqueId).Select(x => x.Stock).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                    affected = await db.StockData.Where(x => x.UniqueId == row.StockUniqueId)
                        .Set(x => x.Stock, x => Math.Round(x.Stock + row.Quantity, 2)).Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
                }
                if (affected != 1) throw new InvalidOperationException($"Stock row for reservation {id} is missing; reservation remains active.");
            }
            await tx.CommitAsync(ct).ConfigureAwait(false);
            if (state != StockReservationState.Consumed) restored = row;
            return new StockReservationResult(id, late ? StockReservationStatus.LateConsumption : state switch
            {
                StockReservationState.Consumed => StockReservationStatus.Consumed,
                StockReservationState.Released => StockReservationStatus.Released,
                _ => StockReservationStatus.Expired,
            }, state, row.ExpiresUtc);
        }, ct).ConfigureAwait(false);
        if (restored is { IsDiscount: false })
        {
            // Derive cache scope from the persisted stock identity, even after configuration changes.
            var store = restored.StockUniqueId == restored.Key.ToString() ? null : restored.StoreAlias;
            await _publisher.PublishAsync(restored.Key, store, restored.StockUniqueId,
                oldStock, decimal.Round(oldStock + restored.Quantity, 2), ct).ConfigureAwait(false);
        }
        return Record(result);
    }

    public async Task<int> ExpireDueAsync(int batchSize, CancellationToken ct = default)
    {
        ValidateBatch(batchSize);
        await using var db = _database.GetDatabase();
        var now = DateTime.UtcNow;
        var candidates = await db.StockReservations.Where(x => x.State == StockReservationState.Active && x.ExpiresUtc <= now &&
                (x.RetryAfterUtc == null || x.RetryAfterUtc <= now))
            .OrderBy(x => x.ExpiresUtc).ThenBy(x => x.Id).Select(x => new { x.Id, x.ExpiryFailures }).Take(batchSize).ToListAsync(ct).ConfigureAwait(false);
        int count = 0;
        foreach (var candidate in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if ((await ExpireAsync(candidate.Id, ct).ConfigureAwait(false)).Status == StockReservationStatus.Expired) count++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                ExpiryFailures.Add(1);
                var retryAfter = DateTime.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(candidate.ExpiryFailures, 6))));
                _logger.LogWarning(ex, "Expiry failed for reservation {ReservationId}; retry after {RetryAfterUtc}", candidate.Id, retryAfter);
                // Persist backoff so one broken stock row cannot permanently starve the next batch.
                await db.StockReservations.Where(x => x.Id == candidate.Id && x.State == StockReservationState.Active)
                    .Set(x => x.RetryAfterUtc, retryAfter).Set(x => x.ExpiryFailures, x => x.ExpiryFailures + 1)
                    .UpdateAsync(ct).ConfigureAwait(false);
            }
        }
        return count;
    }

    public async Task<int> CleanupAsync(DateTime completedBeforeUtc, int batchSize, CancellationToken ct = default)
    {
        ValidateBatch(batchSize);
        await using var db = _database.GetDatabase();
        var ids = await db.StockReservations.Where(x => x.State != StockReservationState.Active && x.CompletedUtc < completedBeforeUtc)
            .OrderBy(x => x.CompletedUtc).Select(x => x.Id).Take(Math.Min(batchSize, 1000)).ToListAsync(ct).ConfigureAwait(false);
        if (ids.Count == 0) return 0;
        return await db.StockReservations.Where(x => ids.Contains(x.Id) && x.State != StockReservationState.Active && x.CompletedUtc < completedBeforeUtc)
            .DeleteAsync(ct).ConfigureAwait(false);
    }

    private static void ValidateBatch(int size)
    {
        if (size < 1 || size > 10000) throw new ArgumentOutOfRangeException(nameof(size));
    }

    private StockReservationResult Record(StockReservationResult result)
    {
        Outcomes.Add(1, new KeyValuePair<string, object?>("status", result.Status.ToString()));
        _logger.LogDebug("Reservation {ReservationId}: {Status}", result.ReservationId, result.Status);
        return result;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    internal static async Task<T> RetryAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await action().ConfigureAwait(false); }
            catch (Exception ex) when (attempt < 5 && IsRetryable(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds((25 << attempt) + RandomNumberGenerator.GetInt32(25)), ct).ConfigureAwait(false);
            }
        }
    }

    private static bool IsRetryable(Exception ex) => ex is DBConcurrencyException ||
        ex is SqliteException { SqliteErrorCode: 5 or 6 } ||
        ex is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 } ||
        ex is SqlException { Number: 1205 or 2601 or 2627 };
}
