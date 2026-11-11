namespace Ekom.Models;

/// <summary>
/// One stock balance to set in a batch. <paramref name="StoreAlias"/> is only used with per-store stock;
/// when it is null the current store is used, as with the single-item overloads.
/// </summary>
public sealed record StockSetRequest(Guid Key, string? StoreAlias, decimal Value);
