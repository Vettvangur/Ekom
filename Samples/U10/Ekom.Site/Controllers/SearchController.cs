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

    private async Task<ActionResult<AlgoliaSearchResponse<AlgoliaProductRecord>>> SearchCoreAsync(
        AlgoliaSearchRequest request,
        CancellationToken ct)
    {
        var response = await _algoliaSearchService.SearchProductsAsync(request, ct).ConfigureAwait(false);
        return Ok(response);
    }

    private async Task<ActionResult<AlgoliaSearchResponse<AlgoliaQuerySuggestionRecord>>> SearchSuggestionsCoreAsync(
        AlgoliaSearchRequest request,
        CancellationToken ct)
    {
        var response = await _algoliaSearchService.SearchQuerySuggestionsAsync(request, ct).ConfigureAwait(false);
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
