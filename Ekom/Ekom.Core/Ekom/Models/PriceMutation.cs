namespace Ekom.Models;

/// <summary>A table-price change. Null Price removes the override; optional comparison prevents stale edits.</summary>
public sealed record PriceMutation(Guid NodeKey, string StoreAlias, string Currency, decimal? Price,
    string PropertyAlias = "price", bool CheckExpected = false, decimal? ExpectedPrice = null);
