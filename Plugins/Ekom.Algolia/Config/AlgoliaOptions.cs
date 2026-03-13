using Ekom.Models;

namespace Ekom.Algolia;

public sealed class AlgoliaOptions
{
    public bool Enabled { get; set; } = true;

    public required string ApplicationId { get; init; }
    public required string AdminApiKey { get; init; }
    public string? InsightsApiKey { get; init; }

    public string Environment { get; init; } = "prod";
    public string? Domain { get; init; }

    public AlgoliaIndexingOptions Indexing { get; set; } = new();
    public AlgoliaEventsOptions Events { get; set; } = new();

    public IReadOnlyCollection<AlgoliaStoreOptions> Stores { get; init; } = [];
}

public sealed class AlgoliaIndexingOptions
{
    public bool Enabled { get; set; } = true;
    public bool Products { get; set; } = true;

    public int BatchSize { get; set; } = 1000;

    public IReadOnlyCollection<string> ProductProperties { get; init; } = [];
    public IReadOnlyCollection<AlgoliaSortedReplicaOptions> SortedReplicas { get; init; } = [];

    public AlgoliaDispatcherOptions Dispatching { get; init; } = new();
}

public sealed class AlgoliaEventsOptions
{
    public bool Enabled { get; set; } = true;
    public bool ViewedProduct { get; set; } = true;
    public bool AddedToCart { get; set; } = true;
    public bool StartedCheckout { get; set; } = true;
    public bool Purchase { get; set; } = true;
}

public sealed class AlgoliaDispatcherOptions
{
    public int MaxBatchSize { get; init; } = 100;
    public int FlushIntervalSeconds { get; init; } = 2;
    public int MaxQueueSize { get; init; } = 10_000;
    public int MaxConcurrency { get; init; } = 2;
}

public sealed class AlgoliaStoreOptions
{
    public required string Alias { get; set; }
    public string? Domain { get; set; }
    public bool IncludeStock { get; set; }
}

public sealed class AlgoliaResolvedStore
{
    public required string Alias { get; init; }
    public string? Domain { get; init; }
    public string? Locale { get; init; }
    public string? Currency { get; init; }
    public bool IncludeStock { get; init; }
    public IReadOnlyList<string> Locales { get; init; } = [];
    public IReadOnlyList<string> Currencies { get; init; } = [];

    public IReadOnlyList<AlgoliaResolvedStore> ExpandIndexTargets()
    {
        var locales = Locales.Count > 0
            ? Locales
            : [Locale ?? string.Empty];
        var currencies = Currencies.Count > 0
            ? Currencies
            : [Currency ?? string.Empty];

        return locales
            .SelectMany(locale => currencies.Select(currency => WithSelection(locale, currency)))
            .DistinctBy(x => new { x.Locale, x.Currency })
            .ToList();
    }

    public AlgoliaResolvedStore WithSelection(string? locale, string? currency)
        => new()
        {
            Alias = Alias,
            Domain = Domain,
            Locale = locale,
            Currency = currency,
            IncludeStock = IncludeStock,
            Locales = Locales,
            Currencies = Currencies
        };
}

public sealed class AlgoliaSortedReplicaOptions
{
    public required string Attribute { get; set; }
    public AlgoliaSortDirection Direction { get; set; } = AlgoliaSortDirection.Asc;
}

public enum AlgoliaSortDirection
{
    Asc,
    Desc
}

public enum AlgoliaIndexKind
{
    Primary,
    Replica,
    QuerySuggestions
}
