namespace Ekom.Shipping;

public interface IShippingDocumentStore
{
    /// <summary>
    /// Durably stores all documents using the supplied deterministic references.
    /// Repeating the same request must be idempotent and must not create duplicate documents.
    /// </summary>
    Task StoreAsync(
        StoreShippingDocumentsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a private document by its opaque reference.</summary>
    Task<ShippingLabel?> GetAsync(
        string reference,
        CancellationToken cancellationToken = default);
}

public interface IShippingCarrierRequiresDocumentStore;
