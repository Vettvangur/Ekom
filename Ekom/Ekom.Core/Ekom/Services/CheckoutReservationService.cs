using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Utilities;
using LinqToDB;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ekom.Services;

/// <summary>Checkout policy and atomic stock completion, including partially reserved orders.</summary>
internal sealed class CheckoutReservationService
{
    private readonly DatabaseFactory _database;
    private readonly Configuration _config;
    private readonly IStockReservationService _reservations;
    private readonly StockChangePublisher _publisher;

    public CheckoutReservationService(DatabaseFactory database, Configuration config,
        IStockReservationService reservations, StockChangePublisher publisher)
    {
        _database = database;
        _config = config;
        _reservations = reservations;
        _publisher = publisher;
    }

    internal async Task<IReadOnlyList<StockReservationRequest>> GetRequirementsAsync(IOrderInfo order, CancellationToken ct, bool includeInventory = true)
    {
        var requests = new List<StockReservationRequest>();
        foreach (var line in order.OrderLines)
        {
            if (includeInventory && !line.Product.Backorder)
            {
                var product = await Catalog.Instance.GetProductAsync(line.ProductKey, order.StoreInfo.Alias, raiseEvent: false, ct: ct)
                    .ConfigureAwait(false) ?? throw new StockException($"Product {line.ProductKey} is missing.");
                if (line.Product.VariantGroups.Any())
                {
                    foreach (var ordered in line.Product.VariantGroups.SelectMany(x => x.Variants))
                    {
                        var variant = await Catalog.Instance.GetVariantAsync(ordered.Key, order.StoreInfo.Alias, ct: ct)
                            .ConfigureAwait(false) ?? throw new StockException($"Variant {ordered.Key} is missing.");
                        requests.Add(new StockReservationRequest
                        {
                            Key = ordered.Key, Quantity = line.Quantity,
                            MinimumRemainingStock = StockBufferHelper.GetConfiguredStockBuffer(product, variant),
                        });
                    }
                }
                else
                {
                    requests.Add(new StockReservationRequest
                    {
                        Key = line.ProductKey, Quantity = line.Quantity,
                        MinimumRemainingStock = StockBufferHelper.GetConfiguredStockBuffer(product),
                    });
                }
            }

            // Match the existing completion policy: one use per discounted line, not per unit.
            // Order-level coupons are marked used separately; they do not debit line discount stock.
            if (line.Discount != null)
            {
                if (!string.IsNullOrEmpty(line.Coupon))
                    requests.Add(new() { Key = line.Discount.Key, Coupon = line.Coupon, Quantity = 1, IsDiscount = true });
                if (line.Discount.HasMasterStock)
                    requests.Add(new() { Key = line.Discount.Key, Quantity = 1, IsDiscount = true });
            }
        }
        return requests.Select(x => x with { StoreAlias = order.StoreInfo.Alias, OrderId = order.UniqueId.ToString() })
            .GroupBy(Identity).Select(g => g.First() with
            {
                Quantity = g.Sum(x => x.Quantity), MinimumRemainingStock = g.Max(x => x.MinimumRemainingStock),
            }).ToList();
    }

    private string StockId(StockReservationRequest request) => request.IsDiscount
        ? request.Coupon == null ? request.Key.ToString() : $"{request.Key}_{request.Coupon}"
        : _config.PerStoreStock ? $"{request.StoreAlias}_{request.Key}" : request.Key.ToString();

    private string Identity(StockReservationRequest request) => $"{request.IsDiscount}:{StockId(request)}";
    private static string Identity(StockReservationData row) => $"{row.IsDiscount}:{row.StockUniqueId}";

    private string Fingerprint(IReadOnlyList<StockReservationRequest> requirements)
        => "checkout-stock:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(requirements.OrderBy(Identity, StringComparer.Ordinal).Select(x => new
            {
                StockIdentity = Identity(x), Quantity = x.Quantity.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
            })))));

    private async Task<List<StockReservationData>> ReadAndVerifyAsync(Repositories.DbContext db, Guid orderId,
        IReadOnlyList<StockReservationRequest> requirements, IEnumerable<string> ids, CancellationToken ct)
    {
        var rows = new List<StockReservationData>();
        var expected = requirements.ToDictionary(Identity);
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            var row = await db.StockReservations.SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
            if (row == null || row.OrderId != orderId.ToString() ||
                !expected.TryGetValue(Identity(row), out var request) || row.Key != request.Key ||
                row.Coupon != request.Coupon || row.StoreAlias != request.StoreAlias || row.Quantity <= 0)
                throw new StockException($"Reservation {id} does not match order {orderId} stock requirements.");
            if (row.PaymentAttemptId?.StartsWith("checkout-stock:", StringComparison.Ordinal) == true &&
                row.PaymentAttemptId != Fingerprint(requirements))
                throw new StockException("Order stock requirements changed after reservation; payment requires reconciliation.");
            if (row.State is StockReservationState.Released or StockReservationState.Expired ||
                (row.State == StockReservationState.Active && row.ExpiresUtc <= DateTime.UtcNow))
                throw new StockException($"Reservation {id} is released or expired; payment requires reconciliation.");
            rows.Add(row);
        }
        foreach (var group in rows.GroupBy(Identity))
        {
            if (group.Sum(x => x.Quantity) > expected[group.Key].Quantity)
                throw new StockException($"Reserved quantity no longer matches order {orderId}.");
        }
        return rows;
    }

    internal async Task PrepareAsync(Guid orderId, IReadOnlyList<StockReservationRequest> requirements,
        ICollection<string> ids, CancellationToken ct, bool createReservations = true, bool validateInventory = true)
    {
        await using var db = _database.GetDatabase();
        if (await db.GetTable<CheckoutStockCompletionData>().AnyAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false))
            throw new StockException("Order stock has already been completed; do not start another payment.");

        // Recover holds committed just before an interrupted order save. Released preparation holds
        // may be retried, but an expired payment hold must never silently become a fresh payment attempt.
        var fingerprint = Fingerprint(requirements);
        var owned = await db.StockReservations.Where(x => x.OrderId == orderId.ToString() &&
            x.PaymentAttemptId != null && x.PaymentAttemptId.StartsWith("checkout-stock:") &&
            x.State != StockReservationState.Released).ToListAsync(ct).ConfigureAwait(false);
        if (owned.Any(x => x.PaymentAttemptId != fingerprint))
            throw new StockException("Order stock requirements changed during payment; reconcile the existing attempt first.");
        foreach (var row in owned)
            if (!ids.Contains(row.Id)) ids.Add(row.Id);
        var existing = await ReadAndVerifyAsync(db, orderId, requirements, ids, ct).ConfigureAwait(false);
        if (existing.Any(x => x.State == StockReservationState.Consumed))
            throw new StockException("Order reservations have already been consumed; do not start another payment.");

        var created = new List<string>();
        try
        {
            foreach (var request in requirements)
            {
                if (!request.IsDiscount && !validateInventory) continue;
                var remaining = request.Quantity - existing.Where(x => Identity(x) == Identity(request)).Sum(x => x.Quantity);
                if (remaining == 0) continue;
                var stockId = StockId(request);
                if (!createReservations)
                {
                    var available = request.IsDiscount
                        ? await db.DiscountStockData.Where(x => x.UniqueId == stockId).Select(x => (decimal)x.Stock).FirstOrDefaultAsync(ct).ConfigureAwait(false)
                        : await db.StockData.Where(x => x.UniqueId == stockId).Select(x => x.Stock).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                    if (available < remaining + request.MinimumRemainingStock)
                        throw new NotEnoughStockException($"Not enough stock for {stockId}.");
                    continue;
                }
                var generation = await db.StockReservations.CountAsync(x => x.OrderId == orderId.ToString() &&
                    x.PaymentAttemptId != null && x.PaymentAttemptId.StartsWith("checkout-stock:") && x.StockUniqueId == stockId &&
                    x.IsDiscount == request.IsDiscount && x.State == StockReservationState.Released, ct).ConfigureAwait(false);
                var result = await _reservations.ReserveAsync(request with
                {
                    Quantity = remaining, OrderId = orderId.ToString(), PaymentAttemptId = fingerprint,
                    IdempotencyKey = $"checkout:{orderId}:{Identity(request)}:{generation}",
                }, ct).ConfigureAwait(false);
                if (result.Status == StockReservationStatus.InsufficientStock)
                    throw new NotEnoughStockException($"Not enough stock for {stockId}.");
                if (result.Status is not (StockReservationStatus.Created or StockReservationStatus.AlreadyExists) ||
                    result.State != StockReservationState.Active || result.ExpiresUtc <= DateTime.UtcNow)
                    throw new StockException($"Unable to prepare checkout reservation: {result.Status}, {result.State}.");
                if (result.Status == StockReservationStatus.Created) created.Add(result.ReservationId!);
                if (!ids.Contains(result.ReservationId!)) ids.Add(result.ReservationId!);
            }
        }
        catch
        {
            await ReleaseAsync(created, CancellationToken.None).ConfigureAwait(false);
            foreach (var id in created) ids.Remove(id);
            throw;
        }
    }

    internal async Task ReleaseAsync(IEnumerable<string> ids, CancellationToken ct)
    {
        var errors = new List<Exception>();
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            try { await _reservations.ReleaseAsync(id, ct).ConfigureAwait(false); }
            catch (Exception ex) { errors.Add(ex); }
        }
        if (errors.Count != 0) throw new AggregateException("Checkout reservation compensation failed; expiry will retry active holds.", errors);
    }

    internal async Task<bool> CompleteStockAsync(Guid orderId, IReadOnlyList<StockReservationRequest> requirements,
        IEnumerable<string> reservationIds, bool validateInventory, CancellationToken ct)
    {
        var ids = reservationIds.Distinct(StringComparer.Ordinal).ToArray();
        var changes = new List<(StockReservationRequest Request, decimal OldStock, decimal NewStock)>();
        var completed = await StockReservationService.RetryAsync(async () =>
        {
            changes.Clear();
            await using var db = _database.GetDatabase();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            if (await db.GetTable<CheckoutStockCompletionData>().AnyAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false))
                return false;
            var rows = await ReadAndVerifyAsync(db, orderId, requirements, ids, ct).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            foreach (var row in rows.Where(x => x.State == StockReservationState.Active))
            {
                var affected = await db.StockReservations.Where(x => x.Id == row.Id &&
                    x.State == StockReservationState.Active && x.ExpiresUtc > now)
                    .Set(x => x.State, StockReservationState.Consumed).Set(x => x.CompletedUtc, now).UpdateAsync(ct).ConfigureAwait(false);
                if (affected != 1) throw new DBConcurrencyException("Checkout reservation changed concurrently.");
            }
            foreach (var request in requirements)
            {
                var remaining = request.Quantity - rows.Where(x => Identity(x) == Identity(request)).Sum(x => x.Quantity);
                if (remaining == 0 || (!request.IsDiscount && !validateInventory)) continue;
                var stockId = StockId(request);
                int affected;
                if (request.IsDiscount)
                {
                    var quantity = checked((int)remaining);
                    affected = await db.DiscountStockData.Where(x => x.UniqueId == stockId && x.Stock >= quantity)
                        .Set(x => x.Stock, x => x.Stock - quantity).Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
                }
                else
                {
                    var old = await db.StockData.Where(x => x.UniqueId == stockId).Select(x => x.Stock).FirstOrDefaultAsync(ct).ConfigureAwait(false);
                    affected = await db.StockData.Where(x => x.UniqueId == stockId && x.Stock >= remaining + request.MinimumRemainingStock)
                        .Set(x => x.Stock, x => Math.Round(x.Stock - remaining, 2)).Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
                    changes.Add((request, old, old - remaining));
                }
                if (affected != 1) throw new NotEnoughStockException($"Not enough stock for {stockId}.");
            }
            await db.InsertAsync(new CheckoutStockCompletionData { OrderId = orderId, CompletedUtc = now }, token: ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
        foreach (var change in changes)
            await _publisher.PublishAsync(change.Request.Key, _config.PerStoreStock ? change.Request.StoreAlias : null,
                StockId(change.Request), change.OldStock, change.NewStock, ct).ConfigureAwait(false);
        return completed;
    }

    internal async Task<bool> IsCompletedAsync(Guid orderId, CancellationToken ct)
    {
        await using var db = _database.GetDatabase();
        return await db.GetTable<CheckoutStockCompletionData>().AnyAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
    }

    // No timed takeover: an expired lease cannot fence a paused owner out of the existing
    // order-save APIs. A crashed owner deliberately requires reconciliation instead.
    internal Task<CheckoutPreparationData> AcquirePreparationAsync(Guid orderId, CancellationToken ct)
        => StockReservationService.RetryAsync(async () =>
        {
            await using var db = _database.GetDatabase();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            var row = await db.GetTable<CheckoutPreparationData>().SingleOrDefaultAsync(x => x.OrderId == orderId, ct).ConfigureAwait(false);
            if (row?.Owner != null)
                throw new StockException("Checkout preparation is already owned by another request; retry after it completes or reconcile an interrupted preparation.");
            var owner = Guid.NewGuid().ToString();
            if (row == null)
            {
                row = new CheckoutPreparationData { OrderId = orderId, Owner = owner };
                await db.InsertAsync(row, token: ct).ConfigureAwait(false);
            }
            else
            {
                var changed = await db.GetTable<CheckoutPreparationData>().Where(x => x.OrderId == orderId && x.Owner == null)
                    .Set(x => x.Owner, owner).UpdateAsync(ct).ConfigureAwait(false);
                if (changed != 1) throw new DBConcurrencyException("Preparation ownership changed.");
                row.Owner = owner;
            }
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return row;
        }, ct);

    internal async Task ProtectPreparationAsync(CheckoutPreparationData ownership, IEnumerable<string> ids, CancellationToken ct)
    {
        var protectedIds = JsonSerializer.Deserialize<string[]>(ownership.ProtectedIds)!
            .Concat(ids).Distinct(StringComparer.Ordinal).ToArray();
        await using var db = _database.GetDatabase();
        var changed = await db.GetTable<CheckoutPreparationData>()
            .Where(x => x.OrderId == ownership.OrderId && x.Owner == ownership.Owner)
            .Set(x => x.ProtectedIds, JsonSerializer.Serialize(protectedIds)).UpdateAsync(ct).ConfigureAwait(false);
        if (changed != 1) throw new DBConcurrencyException("Preparation ownership was lost.");
    }

    internal async Task ReleasePreparationAsync(CheckoutPreparationData ownership, CancellationToken ct)
    {
        await using var db = _database.GetDatabase();
        var changed = await db.GetTable<CheckoutPreparationData>()
            .Where(x => x.OrderId == ownership.OrderId && x.Owner == ownership.Owner)
            .Set(x => x.Owner, (string?)null).UpdateAsync(ct).ConfigureAwait(false);
        if (changed != 1) throw new DBConcurrencyException("Preparation ownership was lost.");
    }
}
