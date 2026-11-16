using Ekom.Models.Payments;
using Ekom.Repositories;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Services;

/// <param name="Migrated">Old payments copied onto their order in this run.</param>
/// <param name="AlreadyPresent">Old payments that were already on their order.</param>
/// <param name="Unresolved">Old payments whose order could not be found. They stay in the old table.</param>
/// <param name="LastReferenceId">The last old payment row that was looked at.</param>
public sealed record LegacyPaymentMigrationResult(int Migrated, int AlreadyPresent, int Unresolved, long LastReferenceId);

/// <summary>
/// Copies the payments of Ekom.Payments 1.0 and earlier (tables EkomPaymentOrders and EkomPayments) onto
/// their Ekom orders. It only adds payment history: no order status, activity log, event, email or checkout
/// is touched, and the old tables are left as they are. It continues where it stopped and can run any number of times.
/// </summary>
internal sealed class LegacyPaymentMigrationService
{
    private const string LegacyTable = "EkomPaymentOrders";
    private const string LegacyDataTable = "EkomPayments";

    private readonly DatabaseFactory _factory;
    private readonly ILogger<LegacyPaymentMigrationService> _logger;

    public LegacyPaymentMigrationService(DatabaseFactory factory, ILogger<LegacyPaymentMigrationService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<LegacyPaymentMigrationResult> MigrateAsync(int batchSize = 200, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        if (!TableExists(db, LegacyTable)) return new LegacyPaymentMigrationResult(0, 0, 0, 0);
        var hasPaymentData = TableExists(db, LegacyDataTable);

        var state = await db.GetTable<OrderPaymentMigrationState>()
            .FirstOrDefaultAsync(x => x.Name == OrderPaymentMigrationState.LegacyPayments, ct).ConfigureAwait(false);
        if (state == null)
        {
            state = new OrderPaymentMigrationState { UpdatedUtc = DateTime.UtcNow };
            await db.InsertAsync(state, token: ct).ConfigureAwait(false);
        }

        int migrated = 0, present = 0, unresolved = 0;
        var unresolvedSamples = new List<long>();

        while (!ct.IsCancellationRequested)
        {
            var after = state.LastReferenceId;
            var rows = await db.GetTable<LegacyPaymentOrderRow>().Where(x => x.ReferenceId > after)
                .OrderBy(x => x.ReferenceId).Take(batchSize).ToListAsync(ct).ConfigureAwait(false);
            if (rows.Count == 0) break;

            var ids = rows.Select(x => x.UniqueId).ToList();
            var paymentData = hasPaymentData
                ? await db.GetTable<LegacyPaymentDataRow>().Where(x => ids.Contains(x.Id)).ToListAsync(ct).ConfigureAwait(false)
                : new List<LegacyPaymentDataRow>();

            foreach (var row in rows)
            {
                switch (await MigrateRowAsync(db, row, paymentData.FirstOrDefault(x => x.Id == row.UniqueId), ct).ConfigureAwait(false))
                {
                    case RowOutcome.Migrated: migrated++; state.Migrated++; break;
                    case RowOutcome.AlreadyPresent: present++; state.AlreadyPresent++; break;
                    default:
                        unresolved++; state.Unresolved++;
                        if (unresolvedSamples.Count < 20) unresolvedSamples.Add(row.ReferenceId);
                        break;
                }
                state.LastReferenceId = row.ReferenceId;
            }

            // Progress is saved per batch, so an interrupted run continues from here.
            state.UpdatedUtc = DateTime.UtcNow;
            await db.UpdateAsync(state, token: ct).ConfigureAwait(false);
        }

        if (migrated + present + unresolved > 0)
        {
            _logger.LogInformation(
                "Copied old payments onto their orders. Migrated: {Migrated}; already present: {AlreadyPresent}; order not found: {Unresolved}; last row: {LastReferenceId}",
                migrated, present, unresolved, state.LastReferenceId);
        }
        if (unresolved > 0)
        {
            _logger.LogWarning(
                "{Unresolved} old payments have no order in this database and stay in {LegacyTable}. First ReferenceIds: {ReferenceIds}",
                unresolved, LegacyTable, string.Join(", ", unresolvedSamples));
        }

        return new LegacyPaymentMigrationResult(migrated, present, unresolved, state.LastReferenceId);
    }

    private enum RowOutcome { Migrated, AlreadyPresent, Unresolved }

    private async Task<RowOutcome> MigrateRowAsync(DbContext db, LegacyPaymentOrderRow row, LegacyPaymentDataRow? data, CancellationToken ct)
    {
        var settings = ParseObject(row.EkomPaymentSettingsData);
        var orderId = OrderId(settings, row.CustomData);
        if (orderId == null) return RowOutcome.Unresolved;

        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        // A provider callback may have copied this payment already; payments are unique by id.
        if (await db.OrderPayments.AnyAsync(x => x.PaymentId == row.UniqueId, ct).ConfigureAwait(false))
            return RowOutcome.AlreadyPresent;

        var order = await db.OrderData.Where(x => x.UniqueId == orderId.Value)
            .Select(x => new { x.OrderNumber, x.StoreAlias }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (order == null) return RowOutcome.Unresolved;

        var createdUtc = ToUtc(row.Date);
        var currency = Text(settings, "Currency");
        var providerName = Text(settings, "PaymentProviderName");

        await db.InsertAsync(new OrderPayment
        {
            PaymentId = row.UniqueId,
            OrderUniqueId = orderId.Value,
            OrderNumber = Text(settings, "OrderNumber") is { Length: > 0 } number ? number : order.OrderNumber,
            AttemptId = Guid.TryParse(Text(settings?["OrderCustomData"] as JsonObject, "ekomPaymentAttemptId"), out var attempt) ? attempt : null,
            StoreAlias = Text(settings?["EkomPropertyKeys"] as JsonObject, "Store") ?? order.StoreAlias,
            ProviderKey = Guid.TryParse(Text(settings, "PaymentProviderKey"), out var key) && key != Guid.Empty ? key : null,
            ProviderName = providerName,
            ProviderType = Text(ParseObject(row.EkomPaymentProviderData), "ekomPaymentProvider"),
            Amount = row.Amount,
            Currency = currency,
            // What the provider looked its payment up by: its own key, or the order id (AltaPay).
            ProviderReference = string.IsNullOrWhiteSpace(row.CustomData) ? null : Limit(row.CustomData, 450),
            Settings = row.EkomPaymentSettingsData,
            ProviderData = row.EkomPaymentProviderData,
            Member = row.Member,
            IPAddress = Limit(row.IPAddress, 45),
            UserAgent = Limit(row.UserAgent, 4000),
            CreatedUtc = createdUtc,
        }, token: ct).ConfigureAwait(false);

        await db.InsertAsync(new OrderPaymentTransaction
        {
            PaymentId = row.UniqueId,
            OrderUniqueId = orderId.Value,
            Type = PaymentTransactionType.Requested,
            Amount = row.Amount,
            Currency = currency,
            Actor = "Customer",
            OccurredUtc = createdUtc,
            ReceivedUtc = createdUtc,
            IdempotencyKey = $"{row.UniqueId:N}:{PaymentTransactionType.Requested}",
        }, token: ct).ConfigureAwait(false);

        if (row.Paid)
        {
            var response = ParseObject(data?.CustomData);
            var capturedUtc = data != null && data.Date > DateTime.MinValue ? ToUtc(data.Date) : createdUtc;
            await db.InsertAsync(new OrderPaymentTransaction
            {
                PaymentId = row.UniqueId,
                OrderUniqueId = orderId.Value,
                Type = PaymentTransactionType.Captured,
                Amount = row.Amount,
                Currency = currency,
                TransactionId = Limit(Find(response, "TransactionNumber", "PayfacReference", "TransactionId"), 200),
                // Providers that stored no response stored the authorization code in its place.
                AuthorizationCode = Limit(response == null && !string.IsNullOrWhiteSpace(data?.CustomData)
                    ? data!.CustomData
                    : Find(response, "AuthorizationNumber", "AuthorizationCode", "AuthCode"), 100),
                MaskedCardNumber = Limit(string.IsNullOrWhiteSpace(data?.CardNumber)
                    ? Find(response, "CardNumberMasked", "CreditCardNumber", "CardNumber")
                    : data!.CardNumber, 30),
                CardBrand = Limit(Find(response, "CardType"), 50),
                PaymentMethod = Limit(string.IsNullOrWhiteSpace(data?.PaymentMethod) ? Find(response, "PaymentMethod") : data!.PaymentMethod, 100),
                Actor = providerName,
                Message = "Copied from a payment made before payments were recorded on the order",
                OccurredUtc = capturedUtc,
                ReceivedUtc = capturedUtc,
                // The key providers use for a capture, so a repeated callback is not stored again.
                IdempotencyKey = $"{row.UniqueId:N}:{PaymentTransactionType.Captured}",
                RawData = response == null ? null : WithoutSecrets(response).ToJsonString(),
            }, token: ct).ConfigureAwait(false);
        }

        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return RowOutcome.Migrated;
    }

    /// <summary>The order a legacy payment belongs to, from the places providers kept it.</summary>
    private static Guid? OrderId(JsonObject? settings, string? customData)
    {
        if (Guid.TryParse(Text(settings, "OrderUniqueId"), out var id) && id != Guid.Empty) return id;
        if (Guid.TryParse(Text(settings?["OrderCustomData"] as JsonObject, "ekomOrderUniqueId"), out id) && id != Guid.Empty) return id;
        return Guid.TryParse(customData, out id) && id != Guid.Empty ? id : null;
    }

    private static JsonObject? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static string? Text(JsonObject? json, string name)
    {
        if (json == null) return null;
        var node = json.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
        return node is JsonValue ? node.ToString() : null;
    }

    /// <summary>The first of the names that has a value, on the response or one level below it.</summary>
    private static string? Find(JsonObject? json, params string[] names)
    {
        if (json == null) return null;
        foreach (var name in names)
        {
            var value = Text(json, name);
            if (!string.IsNullOrWhiteSpace(value) && value != "0") return value;
        }
        foreach (var child in json.Select(x => x.Value).OfType<JsonObject>())
        {
            foreach (var name in names)
            {
                var value = Text(child, name);
                if (!string.IsNullOrWhiteSpace(value) && value != "0") return value;
            }
        }
        return null;
    }

    /// <summary>Old responses were stored whole. Signatures, hashes and card tokens are not kept.</summary>
    private static JsonObject WithoutSecrets(JsonObject json)
    {
        var copy = new JsonObject();
        foreach (var (name, value) in json)
        {
            if (name.Contains("signature", StringComparison.OrdinalIgnoreCase)
                || name.Contains("hash", StringComparison.OrdinalIgnoreCase)
                || name.Contains("token", StringComparison.OrdinalIgnoreCase)) continue;
            copy[name] = value is JsonObject child ? WithoutSecrets(child) : value?.DeepClone();
        }
        return copy;
    }

    // Old rows were stamped with the server's local time.
    private static DateTime ToUtc(DateTime local)
        => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    private static string? Limit(string? value, int length)
        => value != null && value.Length > length ? value[..length] : value;

    private bool TableExists(DbContext db, string table)
    {
        var query = _factory.IsSqlServer
            ? $"SELECT COUNT(*) FROM sys.tables WHERE name = '{table}'"
            : $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'";
        return db.Execute<int>(query) > 0;
    }
}
