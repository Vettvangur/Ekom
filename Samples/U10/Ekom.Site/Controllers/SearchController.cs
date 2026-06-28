using Algolia.Search.Models.Search;
using Ekom.Algolia.Models.Indexing;
using Ekom.Algolia.Models.Search;
using Ekom.Algolia.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Site.Controllers;

[ApiController]
[Route("ekom/search")]
public sealed class SearchController : ControllerBase
{
    private readonly IAlgoliaSearchService _algoliaSearchService;

    public SearchController(IAlgoliaSearchService algoliaSearchService)
    {
        _algoliaSearchService = algoliaSearchService;
    }

    [HttpGet("products")]
    public Task<ActionResult<AlgoliaSearchResponse<AlgoliaProductRecord>>> SearchProductsAsync(
        [FromQuery] string storeAlias,
        [FromQuery] string? query,
        [FromQuery] string? locale = null,
        [FromQuery] string? currency = null,
        [FromQuery] string? filters = null,
        [FromQuery] int? page = null,
        [FromQuery] int? hitsPerPage = null,
        [FromQuery] bool bypassCache = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return Task.FromResult<ActionResult<AlgoliaSearchResponse<AlgoliaProductRecord>>>(BadRequest("Store alias is required."));

        return SearchCoreAsync(
            new AlgoliaSearchRequest
            {
                StoreAlias = storeAlias,
                Locale = locale,
                Currency = currency,
                BypassCache = bypassCache,
                Query = new SearchForHits
                {
                    Query = query,
                    Filters = filters,
                    Page = page,
                    HitsPerPage = hitsPerPage
                }
            },
            ct);
    }

    [HttpPost("products")]
    public Task<ActionResult<AlgoliaSearchResponse<AlgoliaProductRecord>>> SearchProductsAdvancedAsync(
        [FromBody] SearchProductsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return Task.FromResult<ActionResult<AlgoliaSearchResponse<AlgoliaProductRecord>>>(BadRequest("Store alias is required."));

        return SearchCoreAsync(
            new AlgoliaSearchRequest
            {
                StoreAlias = request.StoreAlias,
                Locale = request.Locale,
                Currency = request.Currency,
                BypassCache = request.BypassCache,
                Query = request.Query
            },
            ct);
    }

    [HttpGet("categories")]
    public Task<ActionResult<AlgoliaSearchResponse<AlgoliaCategoryRecord>>> SearchCategoriesAsync(
        [FromQuery] string storeAlias,
        [FromQuery] string? query,
        [FromQuery] string? locale = null,
        [FromQuery] int? page = null,
        [FromQuery] int? hitsPerPage = null,
        [FromQuery] bool bypassCache = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return Task.FromResult<ActionResult<AlgoliaSearchResponse<AlgoliaCategoryRecord>>>(BadRequest("Store alias is required."));

        return SearchCategoriesCoreAsync(
            new AlgoliaSearchRequest
            {
                StoreAlias = storeAlias,
                Locale = locale,
                BypassCache = bypassCache,
                Query = new SearchForHits
                {
                    Query = query,
                    Page = page,
                    HitsPerPage = hitsPerPage
                }
            },
            ct);
    }

    [HttpPost("categories")]
    public Task<ActionResult<AlgoliaSearchResponse<AlgoliaCategoryRecord>>> SearchCategoriesAdvancedAsync(
        [FromBody] SearchProductsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return Task.FromResult<ActionResult<AlgoliaSearchResponse<AlgoliaCategoryRecord>>>(BadRequest("Store alias is required."));

        return SearchCategoriesCoreAsync(
            new AlgoliaSearchRequest
            {
                StoreAlias = request.StoreAlias,
                Locale = request.Locale,
                BypassCache = request.BypassCache,
                Query = request.Query
            },
            ct);
    }

    [HttpGet("suggestions")]
    public Task<ActionResult<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>>> SearchQuerySuggestionsAsync(
        [FromQuery] string storeAlias,
        [FromQuery] string? query,
        [FromQuery] string? locale = null,
        [FromQuery] string? currency = null,
        [FromQuery] int? page = null,
        [FromQuery] int? hitsPerPage = null,
        [FromQuery] bool bypassCache = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return Task.FromResult<ActionResult<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>>>(BadRequest("Store alias is required."));

        return SearchSuggestionsCoreAsync(
            new AlgoliaSearchRequest
            {
                StoreAlias = storeAlias,
                Locale = locale,
                Currency = currency,
                BypassCache = bypassCache,
                Query = new SearchForHits
                {
                    Query = query,
                    Page = page,
                    HitsPerPage = hitsPerPage
                }
            },
            ct);
    }

    [HttpPost("suggestions")]
    public Task<ActionResult<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>>> SearchQuerySuggestionsAdvancedAsync(
        [FromBody] SearchProductsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return Task.FromResult<ActionResult<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>>>(BadRequest("Store alias is required."));

        return SearchSuggestionsCoreAsync(
            new AlgoliaSearchRequest
            {
                StoreAlias = request.StoreAlias,
                Locale = request.Locale,
                Currency = request.Currency,
                BypassCache = request.BypassCache,
                Query = request.Query
            },
            ct);
    }

    [HttpPost("federated")]
    public Task<ActionResult<AlgoliaFederatedSearchResponse>> SearchFederatedAsync(
        [FromBody] AlgoliaFederatedSearchRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.StoreAlias))
            return Task.FromResult<ActionResult<AlgoliaFederatedSearchResponse>>(BadRequest("Store alias is required."));

        if (request.Targets is null || request.Targets.Count == 0)
            return Task.FromResult<ActionResult<AlgoliaFederatedSearchResponse>>(BadRequest("At least one search target is required."));

        foreach (var target in request.Targets)
        {
            if (string.IsNullOrWhiteSpace(target.Key))
                return Task.FromResult<ActionResult<AlgoliaFederatedSearchResponse>>(BadRequest("Each search target requires a key."));

            if (target.Query is null)
                return Task.FromResult<ActionResult<AlgoliaFederatedSearchResponse>>(BadRequest("Each search target requires a query."));

            if (target.Kind == AlgoliaFederatedSearchTargetKind.Content && string.IsNullOrWhiteSpace(target.ContentIndexName))
                return Task.FromResult<ActionResult<AlgoliaFederatedSearchResponse>>(BadRequest("Content targets require a content index name."));
        }

        return SearchFederatedCoreAsync(request, ct);
    }

    private async Task<ActionResult<AlgoliaSearchResponse<AlgoliaProductRecord>>> SearchCoreAsync(
        AlgoliaSearchRequest request,
        CancellationToken ct)
    {
        var response = await _algoliaSearchService.SearchProductsAsync(request, ct).ConfigureAwait(false);
        return Ok(response);
    }

    private async Task<ActionResult<AlgoliaSearchResponse<AlgoliaCategoryRecord>>> SearchCategoriesCoreAsync(
        AlgoliaSearchRequest request,
        CancellationToken ct)
    {
        var response = await _algoliaSearchService.SearchCategoriesAsync(request, ct).ConfigureAwait(false);
        return Ok(response);
    }

    private async Task<ActionResult<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>>> SearchSuggestionsCoreAsync(
        AlgoliaSearchRequest request,
        CancellationToken ct)
    {
        var response = await _algoliaSearchService.SearchQuerySuggestionsAsync(request, ct).ConfigureAwait(false);
        return Ok(response);
    }

    private async Task<ActionResult<AlgoliaFederatedSearchResponse>> SearchFederatedCoreAsync(
        AlgoliaFederatedSearchRequest request,
        CancellationToken ct)
    {
        var response = await _algoliaSearchService.FederatedSearchAsync(request, ct).ConfigureAwait(false);
        return Ok(response);
    }
}

public sealed record SearchProductsRequest
{
    public string StoreAlias { get; init; } = string.Empty;
    public string? Locale { get; init; }
    public string? Currency { get; init; }
    public bool BypassCache { get; init; }
    public SearchForHits Query { get; init; } = new();
}
