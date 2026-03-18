using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Services;

internal interface IAlgoliaQueryClient
{
    Task<SearchResponses<T>> SearchAsync<T>(SearchMethodParams request, CancellationToken ct = default) where T : class;
}

internal sealed class AlgoliaQueryClient : IAlgoliaQueryClient
{
    private readonly SearchClient _client;

    public AlgoliaQueryClient(IOptions<AlgoliaOptions> options)
    {
        var opt = options.Value;
        _client = new SearchClient(opt.ApplicationId, opt.SearchApiKey);
    }

    public Task<SearchResponses<T>> SearchAsync<T>(SearchMethodParams request, CancellationToken ct = default) where T : class
        => _client.SearchAsync<T>(request, cancellationToken: ct);
}
