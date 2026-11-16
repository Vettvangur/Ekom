using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ekom.Models;
using Ekom.Models.Shipments;
using Ekom.Services;
using Microsoft.Extensions.Logging;

namespace Ekom.Shipping;

internal sealed class ShippingFulfillmentService : IShippingFulfillmentService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> OrderLocks = new();
    private readonly IShippingOrderReader _orderApi;
    private readonly IShippingFulfillmentCarrierRegistry _carriers;
    private readonly IShippingOrderMapper _mapper;
    private readonly IEnumerable<IShippingAutomationRule> _automationRules;
    private readonly IReadOnlyList<IShippingDocumentStore> _documentStores;
    private readonly IOrderShipments _shipments;
    private readonly ILogger<ShippingFulfillmentService> _logger;

    public ShippingFulfillmentService(
        IShippingOrderReader orderApi,
        IShippingFulfillmentCarrierRegistry carriers,
        IShippingOrderMapper mapper,
        IEnumerable<IShippingAutomationRule> automationRules,
        IEnumerable<IShippingDocumentStore> documentStores,
        IOrderShipments shipments,
        ILogger<ShippingFulfillmentService> logger)
    {
        _orderApi = orderApi;
        _carriers = carriers;
        _mapper = mapper;
        _automationRules = automationRules;
        _documentStores = documentStores.ToArray();
        _shipments = shipments;
        _logger = logger;
    }

    public async Task<ShippingFulfillmentRecord?> CreateAutomaticAsync(
        IOrderInfo order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        var configuration = EkomShippingMethodConfiguration.FromProperties(
            order.ShippingProvider.Key,
            order.ShippingProvider.Properties);
        if (configuration is null)
        {
            return null;
        }

        var carrier = _carriers.Carriers.FirstOrDefault(candidate =>
            string.Equals(candidate.Alias, configuration.CarrierAlias, StringComparison.OrdinalIgnoreCase));
        if (carrier is null ||
            carrier.GetFulfillmentMode(configuration.AccountReference) != ShippingFulfillmentMode.Automatic)
        {
            return null;
        }

        foreach (var rule in _automationRules)
        {
            if (!await rule.CanCreateShipmentAsync(order, cancellationToken).ConfigureAwait(false))
            {
                return null;
            }
        }

        return await CreateInternalAsync(order, retryFailed: false, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ShippingFulfillmentRecord> CreateAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await GetRequiredOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        return await CreateInternalAsync(order, retryFailed: false, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ShippingFulfillmentRecord> RetryAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await GetRequiredOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        return await CreateInternalAsync(order, retryFailed: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ShippingFulfillmentRecord?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderApi.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return null;
        }

        var record = await LoadAsync(order, cancellationToken).ConfigureAwait(false);
        if (record?.State != ShipmentState.Deleting || !IsStale(record))
        {
            return record;
        }

        var semaphore = OrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            record = await LoadAsync(order, cancellationToken).ConfigureAwait(false);
            return record?.State == ShipmentState.Deleting && IsStale(record)
                ? await MarkDeletionOutcomeUnknownAsync(order, record).ConfigureAwait(false)
                : record;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<ShippingLabel> GetLabelAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await GetRequiredOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        var record = await LoadAsync(order, cancellationToken).ConfigureAwait(false)
            ?? throw new ShippingException("This order does not have a shipment.");
        var storedLabels = record.Documents?.Where(document =>
            string.Equals(document.TypeCode, "label", StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];
        if (storedLabels.Length > 0)
        {
            var documentStore = GetDocumentStore();
            var labels = new List<ShippingLabel>(storedLabels.Length);
            foreach (var storedLabel in storedLabels)
            {
                labels.Add(await documentStore.GetAsync(storedLabel.Reference, cancellationToken).ConfigureAwait(false)
                    ?? throw new ShippingException($"Stored shipping label '{storedLabel.Reference}' could not be found."));
            }

            if (labels.Count == 1)
            {
                return labels[0];
            }

            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var index = 0; index < labels.Count; index++)
                {
                    var label = labels[index];
                    var entry = archive.CreateEntry($"{index + 1}-{label.FileName}", CompressionLevel.Fastest);
                    await using var entryStream = entry.Open();
                    await entryStream.WriteAsync(label.Content, cancellationToken).ConfigureAwait(false);
                }
            }

            return new ShippingLabel(stream.ToArray(), "application/zip", "shipping-labels.zip");
        }

        if (record.State != ShipmentState.Created || string.IsNullOrWhiteSpace(record.ShipmentId))
        {
            throw new ShippingException("A label is only available after shipment creation.");
        }

        var carrier = _carriers.GetRequired(record.CarrierAlias);
        return await carrier.GetLabelAsync(
            record.AccountReference,
            record.ShipmentId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShippingShipmentDocument> GetShipmentAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await GetRequiredOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        var record = await LoadAsync(order, cancellationToken).ConfigureAwait(false)
            ?? throw new ShippingException("This order does not have a shipment.");
        if (string.IsNullOrWhiteSpace(record.ShipmentId))
        {
            throw new ShippingException("This order does not have a carrier shipment ID.");
        }

        var carrier = _carriers.GetRequired(record.CarrierAlias);
        if (carrier is not IShippingShipmentLookupCarrier lookupCarrier)
        {
            throw new ShippingException($"Shipping carrier '{record.CarrierAlias}' does not support shipment lookup.");
        }

        return await lookupCarrier.GetShipmentAsync(
            record.AccountReference,
            record.ShipmentId,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ShippingFulfillmentRecord> DeleteAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var semaphore = OrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var order = await GetRequiredOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            var record = await LoadAsync(order, cancellationToken).ConfigureAwait(false)
                ?? throw new ShippingException("This order does not have a shipment.");
            if (record.State == ShipmentState.Deleting && IsStale(record))
            {
                return await MarkDeletionOutcomeUnknownAsync(order, record).ConfigureAwait(false);
            }

            if (record.State != ShipmentState.Created || string.IsNullOrWhiteSpace(record.ShipmentId))
            {
                throw new ShippingException("Only a created shipment can be deleted.");
            }

            var carrier = _carriers.GetRequired(record.CarrierAlias);
            if (carrier is not IShippingShipmentDeletionCarrier deletionCarrier)
            {
                throw new ShippingException($"Shipping carrier '{record.CarrierAlias}' does not support shipment deletion.");
            }

            await SaveStateAsync(order, record.RecordId, ShipmentState.Deleting, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await deletionCarrier.DeleteShipmentAsync(
                    record.AccountReference,
                    record.ShipmentId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ShipmentOutcomeUnknownException exception)
            {
                return await SaveDeletionFailureAsync(order, record, ShipmentState.DeleteOutcomeUnknown, exception)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                await SaveDeletionFailureAsync(order, record, ShipmentState.DeleteOutcomeUnknown, exception)
                    .ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                await SaveDeletionFailureAsync(order, record, ShipmentState.Created, exception).ConfigureAwait(false);
                throw;
            }

            try
            {
                return await SaveStateAsync(order, record.RecordId, ShipmentState.Deleted, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return await SaveDeletionFailureAsync(order, record, ShipmentState.DeleteOutcomeUnknown, exception)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<ShippingFulfillmentRecord> CreateInternalAsync(
        IOrderInfo initialOrder,
        bool retryFailed,
        CancellationToken cancellationToken)
    {
        var semaphore = OrderLocks.GetOrAdd(initialOrder.UniqueId, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var order = await _orderApi.GetOrderAsync(initialOrder.UniqueId, cancellationToken)
                .ConfigureAwait(false) ?? initialOrder;
            var configuration = EkomShippingMethodConfiguration.FromProperties(
                order.ShippingProvider.Key,
                order.ShippingProvider.Properties)
                ?? throw new ShippingConfigurationException("The order shipping method has no carrier configuration.");
            var existing = await LoadAsync(order, cancellationToken).ConfigureAwait(false);
            if (existing?.State == ShipmentState.Created)
            {
                return existing;
            }

            var staleBefore = DateTime.UtcNow.AddMinutes(-10);
            if (existing?.State == ShipmentState.Submitting && existing.UpdatedUtc < staleBefore)
            {
                return await SaveStateAsync(
                    order,
                    existing.RecordId,
                    ShipmentState.OutcomeUnknown,
                    error: "Shipment submission was interrupted and requires reconciliation.",
                    cancellationToken: CancellationToken.None).ConfigureAwait(false);
            }

            if (existing?.State == ShipmentState.Deleting && existing.UpdatedUtc < staleBefore)
            {
                return await MarkDeletionOutcomeUnknownAsync(order, existing).ConfigureAwait(false);
            }

            if (existing?.State is ShipmentState.Deleting or ShipmentState.DeleteOutcomeUnknown)
            {
                throw new ShippingException(
                    $"Shipment creation cannot continue while its state is {existing.State}.");
            }

            if ((existing?.State == ShipmentState.Processing && existing.UpdatedUtc >= staleBefore) ||
                existing?.State is ShipmentState.Submitting or ShipmentState.OutcomeUnknown)
            {
                throw new ShippingException(
                    $"Shipment creation cannot continue while its state is {existing.State}.");
            }

            if (existing?.State == ShipmentState.Failed && !retryFailed)
            {
                throw new ShippingException("The previous shipment attempt failed. Use the explicit retry operation.");
            }

            if (retryFailed && existing?.State != ShipmentState.Failed)
            {
                throw new ShippingException("Only a shipment with state Failed can be retried.");
            }

            var carrier = _carriers.GetRequired(configuration.CarrierAlias);

            // A deleted shipment stays in the order's history; the next one is a shipment of its own.
            ShippingFulfillmentRecord record;
            if (existing is null || existing.State == ShipmentState.Deleted)
            {
                var created = await _shipments.CreateAsync(
                    NewShipment(order, configuration) with { State = ShipmentState.Processing, Attempts = 1 },
                    cancellationToken).ConfigureAwait(false);
                record = ToRecord(order, created);
            }
            else
            {
                record = await SaveStateAsync(
                    order,
                    existing.RecordId,
                    ShipmentState.Processing,
                    attempts: existing.AttemptCount + 1,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            var shipmentKey = record.RecordId;
            var bookingReference = record.BookingReference;

            IShippingDocumentStore? documentStore = null;
            if (carrier is IShippingCarrierRequiresDocumentStore)
            {
                try
                {
                    documentStore = GetDocumentStore();
                }
                catch (Exception exception)
                {
                    return await SaveFailureAsync(order, shipmentKey, ShipmentState.Failed, exception).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrWhiteSpace(bookingReference))
            {
                try
                {
                    bookingReference = await carrier.ReserveBookingReferenceAsync(
                        configuration.AccountReference,
                        cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(bookingReference))
                    {
                        await SaveStateAsync(
                            order,
                            shipmentKey,
                            ShipmentState.Processing,
                            bookingReference: bookingReference,
                            cancellationToken: cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    return await SaveFailureAsync(order, shipmentKey, ShipmentState.Failed, exception).ConfigureAwait(false);
                }
            }

            ShipmentBookingRequest request;
            try
            {
                request = _mapper.Map(order, configuration, bookingReference);
                await SaveStateAsync(order, shipmentKey, ShipmentState.Submitting, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return await SaveFailureAsync(order, shipmentKey, ShipmentState.Failed, exception).ConfigureAwait(false);
            }

            ShipmentBookingResult result;
            try
            {
                result = await carrier.CreateShipmentAsync(
                    configuration.AccountReference,
                    request,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ShipmentOutcomeUnknownException exception)
            {
                return await SaveFailureAsync(order, shipmentKey, ShipmentState.OutcomeUnknown, exception).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception)
            {
                await SaveFailureAsync(order, shipmentKey, ShipmentState.OutcomeUnknown, exception).ConfigureAwait(false);
                throw;
            }
            catch (Exception exception)
            {
                return await SaveFailureAsync(order, shipmentKey, ShipmentState.Failed, exception).ConfigureAwait(false);
            }

            IReadOnlyList<StoredShippingDocument> storedDocuments = [];
            if (result.Documents is { Count: > 0 })
            {
                var storageItems = result.Documents.Select((document, index) =>
                {
                    var stored = new StoredShippingDocument(
                        DocumentReference(order.UniqueId, configuration.CarrierAlias, result.ShipmentId, document, index),
                        document.TypeCode,
                        document.Format,
                        document.ContentType,
                        document.FileName,
                        document.PackageReferenceNumber);
                    return new ShippingDocumentStorageItem(stored, document.Content);
                }).ToArray();
                storedDocuments = storageItems.Select(item => item.Document).ToArray();
                try
                {
                    documentStore ??= GetDocumentStore();
                    await SaveStateAsync(
                        order,
                        shipmentKey,
                        ShipmentState.OutcomeUnknown,
                        result: result,
                        error: "Shipment documents are awaiting durable storage.",
                        documents: storedDocuments,
                        cancellationToken: CancellationToken.None).ConfigureAwait(false);
                    await documentStore.StoreAsync(
                        new StoreShippingDocumentsRequest(
                            order.UniqueId,
                            configuration.CarrierAlias,
                            result.ShipmentId,
                            storageItems),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception)
                {
                    await SaveFailureAsync(order, shipmentKey, ShipmentState.OutcomeUnknown, exception, result, storedDocuments)
                        .ConfigureAwait(false);
                    throw;
                }
                catch (Exception exception)
                {
                    return await SaveFailureAsync(order, shipmentKey, ShipmentState.OutcomeUnknown, exception, result, storedDocuments)
                        .ConfigureAwait(false);
                }
            }

            try
            {
                return await SaveStateAsync(
                    order,
                    shipmentKey,
                    ShipmentState.Created,
                    result: result,
                    documents: storedDocuments,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return await SaveFailureAsync(order, shipmentKey, ShipmentState.OutcomeUnknown, exception, result)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<ShippingFulfillmentRecord> SaveFailureAsync(
        IOrderInfo order,
        Guid shipmentKey,
        ShipmentState state,
        Exception exception,
        ShipmentBookingResult? result = null,
        IReadOnlyList<StoredShippingDocument>? documents = null)
    {
        _logger.LogError(
            exception,
            "Shipment creation for order {OrderId} entered state {ShipmentState}",
            order.UniqueId,
            state);
        return await SaveStateAsync(
            order,
            shipmentKey,
            state,
            result: result,
            error: SafeError(exception),
            documents: documents,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<ShippingFulfillmentRecord> SaveDeletionFailureAsync(
        IOrderInfo order,
        ShippingFulfillmentRecord record,
        ShipmentState state,
        Exception exception)
    {
        _logger.LogError(
            exception,
            "Shipment deletion for order {OrderId} entered state {ShipmentState}",
            order.UniqueId,
            state);
        return await SaveStateAsync(
            order,
            record.RecordId,
            state,
            error: SafeError(exception),
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
    }

    private Task<ShippingFulfillmentRecord> MarkDeletionOutcomeUnknownAsync(
        IOrderInfo order,
        ShippingFulfillmentRecord record) =>
        SaveDeletionFailureAsync(
            order,
            record,
            ShipmentState.DeleteOutcomeUnknown,
            new ShippingException("Shipment deletion was interrupted and requires reconciliation."));

    private static bool IsStale(ShippingFulfillmentRecord record) =>
        record.UpdatedUtc < DateTime.UtcNow.AddMinutes(-10);

    /// <summary>Stores a step of the shipment in Ekom's shipment records. Values left out keep what is stored.</summary>
    private async Task<ShippingFulfillmentRecord> SaveStateAsync(
        IOrderInfo order,
        Guid shipmentKey,
        ShipmentState state,
        int? attempts = null,
        string? bookingReference = null,
        ShipmentBookingResult? result = null,
        string? error = null,
        IReadOnlyList<StoredShippingDocument>? documents = null,
        CancellationToken cancellationToken = default)
    {
        var stored = await _shipments.RecordAsync(
            new ShipmentUpdate
            {
                ShipmentId = shipmentKey,
                State = state,
                Attempts = attempts,
                ShipmentReference = result?.BookingReference ?? bookingReference,
                CarrierShipmentId = result?.ShipmentId,
                TrackingNumber = result?.TrackingNumber,
                TrackingUrl = result?.TrackingUrl,
                ServiceName = result?.ServiceName,
                CarrierStatus = result?.CarrierStatus,
                Details = result?.Details,
                Documents = documents is null ? null : JsonSerializer.Serialize(documents),
                Error = error,
            },
            cancellationToken).ConfigureAwait(false);
        return ToRecord(order, stored.Shipment);
    }

    private async Task<IOrderInfo> GetRequiredOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await _orderApi.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false)
        ?? throw new ShippingException($"Order '{orderId}' was not found.");

    /// <summary>
    /// The order's current carrier shipment. An order whose shipment was stored on the shipping provider's
    /// custom data, before Ekom kept shipment records, has it copied over the first time it is read.
    /// </summary>
    private async Task<ShippingFulfillmentRecord?> LoadAsync(IOrderInfo order, CancellationToken cancellationToken)
    {
        var current = await _shipments.GetCurrentAsync(order.UniqueId, cancellationToken).ConfigureAwait(false);
        if (current is null)
        {
            var legacy = ReadLegacyShipment(order);
            if (legacy is null)
            {
                return null;
            }

            current = await _shipments.CreateAsync(legacy, cancellationToken).ConfigureAwait(false);
        }

        // A shipment entered by hand has no carrier to book, print or delete it with.
        return string.IsNullOrWhiteSpace(current.CarrierAlias) ? null : ToRecord(order, current);
    }

    private static ShippingFulfillmentRecord ToRecord(IOrderInfo order, OrderShipment shipment)
    {
        IReadOnlyList<StoredShippingDocument> documents;
        try
        {
            documents = shipment.GetDocuments<StoredShippingDocument>();
        }
        catch (JsonException)
        {
            throw new ShippingException("Stored shipment document metadata is invalid.");
        }

        return new ShippingFulfillmentRecord(
            shipment.OrderUniqueId,
            shipment.ProviderKey ?? order.ShippingProvider?.Key ?? Guid.Empty,
            shipment.CarrierAlias ?? string.Empty,
            shipment.AccountReference ?? string.Empty,
            shipment.ServiceId ?? string.Empty,
            shipment.State,
            shipment.ShipmentReference,
            shipment.CarrierShipmentId,
            shipment.TrackingNumber,
            shipment.Attempts,
            shipment.LastError,
            order.CreateDate.ToUniversalTime(),
            DateTime.SpecifyKind(shipment.UpdatedUtc, DateTimeKind.Utc),
            documents)
        {
            RecordId = shipment.ShipmentId,
            TrackingUrl = shipment.TrackingUrl,
        };
    }

    private static NewOrderShipment NewShipment(IOrderInfo order, EkomShippingMethodConfiguration configuration)
    {
        var provider = order.ShippingProvider;
        var data = provider.CustomData;
        return new NewOrderShipment
        {
            OrderUniqueId = order.UniqueId,
            OrderNumber = order.OrderNumber,
            StoreAlias = order.StoreInfo?.Alias,
            ProviderKey = provider.Key,
            ProviderName = provider.Title,
            CarrierAlias = configuration.CarrierAlias,
            AccountReference = configuration.AccountReference,
            ServiceId = configuration.ServiceId,
            ServiceName = Get(data, EkomShippingPropertyAliases.CustomServiceName),
            PickupLocationId = Get(data, EkomShippingPropertyAliases.CustomPickupLocationId, "customshippingDroppLocationId"),
            PickupLocationName = Get(data, EkomShippingPropertyAliases.CustomPickupLocationName),
            PickupLocationAddress = Get(data, EkomShippingPropertyAliases.CustomPickupLocationAddress),
            PickupLocationPostalCode = Get(data, EkomShippingPropertyAliases.CustomPickupLocationPostalCode),
            PickupLocationCity = Get(data, EkomShippingPropertyAliases.CustomPickupLocationCity),
            Amount = ShippingAmount(provider),
            Currency = order.StoreInfo?.Currency?.ISOCurrencySymbol,
        };
    }

    /// <summary>What the customer paid for shipping. An unreadable price never stops a shipment.</summary>
    private static decimal? ShippingAmount(OrderedShippingProvider provider)
    {
        try
        {
            return provider.Price?.WithVat?.Value;
        }
        catch (Exception exception) when (exception is NullReferenceException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a shipment that was stored on the shipping provider's custom data, which is where shipments lived
    /// before Ekom kept shipment records.
    /// </summary>
    internal static NewOrderShipment? ReadLegacyShipment(IOrderInfo order)
    {
        var shippingProvider = order.ShippingProvider;
        if (shippingProvider is null)
        {
            return null;
        }

        var data = shippingProvider.CustomData;
        if (!TryGet(data, EkomShippingPropertyAliases.CustomShipmentState, out var stateValue) ||
            !Enum.TryParse<ShipmentState>(WebUtility.HtmlDecode(stateValue), true, out var state))
        {
            if (!TryGet(data, "customshippingDroppOrderId", out var legacyId) ||
                string.IsNullOrWhiteSpace(legacyId))
            {
                return null;
            }

            state = ShipmentState.Created;
        }

        var carrierAlias = Get(data, EkomShippingPropertyAliases.CustomCarrierAlias);
        var accountReference = Get(data, EkomShippingPropertyAliases.CustomAccountReference);
        var serviceId = Get(data, EkomShippingPropertyAliases.CustomServiceId);
        var configuration = !string.IsNullOrWhiteSpace(carrierAlias) &&
                            !string.IsNullOrWhiteSpace(accountReference) &&
                            !string.IsNullOrWhiteSpace(serviceId)
            ? new EkomShippingMethodConfiguration(carrierAlias, accountReference, serviceId)
            : EkomShippingMethodConfiguration.FromProperties(
                shippingProvider.Key,
                shippingProvider.Properties);
        if (configuration is null)
        {
            return null;
        }

        _ = int.TryParse(Get(data, EkomShippingPropertyAliases.CustomShipmentAttempts), out var attempts);
        _ = DateTime.TryParse(
            Get(data, EkomShippingPropertyAliases.CustomShipmentUpdatedUtc),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var updatedUtc);

        return NewShipment(order, configuration) with
        {
            // The same id every time, so an order opened twice at once is copied once.
            ShipmentId = order.UniqueId,
            State = state,
            Attempts = attempts,
            ShipmentReference = Get(data, EkomShippingPropertyAliases.CustomShipmentReference, "customshippingDroppBarcode"),
            CarrierShipmentId = Get(data, EkomShippingPropertyAliases.CustomShipmentId, "customshippingDroppOrderId"),
            TrackingNumber = Get(data, EkomShippingPropertyAliases.CustomTrackingNumber),
            LastError = Get(data, EkomShippingPropertyAliases.CustomShipmentLastError),
            Documents = Get(data, EkomShippingPropertyAliases.CustomShipmentDocuments),
            CreatedUtc = updatedUtc == default ? order.UpdateDate.ToUniversalTime() : updatedUtc.ToUniversalTime(),
            Message = "Copied from the order's shipping custom data.",
            Imported = true,
        };
    }

    private IShippingDocumentStore GetDocumentStore()
    {
        if (_documentStores.Count != 1)
        {
            throw new ShippingConfigurationException(
                _documentStores.Count == 0
                    ? "A private shipping document store is required."
                    : "Only one private shipping document store can be registered.");
        }

        return _documentStores[0];
    }

    private static string DocumentReference(
        Guid orderId,
        string carrierAlias,
        string shipmentId,
        ShippingCreationDocument document,
        int index)
    {
        var source = Encoding.UTF8.GetBytes(
            $"{orderId:N}|{carrierAlias}|{shipmentId}|{index}|{document.TypeCode}|{document.PackageReferenceNumber}");
        return $"shipping/{Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant()}";
    }

    private static bool TryGet(
        IReadOnlyDictionary<string, string>? data,
        string key,
        out string value)
    {
        if (data is not null && data.TryGetValue(key, out var found) && found is not null)
        {
            value = found;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? Get(IReadOnlyDictionary<string, string>? data, params string[] keys)
    {
        if (data is null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return WebUtility.HtmlDecode(value);
            }
        }

        return null;
    }

    private static string SafeError(Exception exception)
    {
        var message = exception switch
        {
            InvalidShippingSelectionException => exception.Message,
            ShippingConfigurationException => exception.Message,
            ShippingProviderException => exception.Message,
            ShippingException => exception.Message,
            _ => "Unexpected shipment processing error.",
        };
        return message.Length <= 500 ? message : message[..500];
    }
}
