using Ekom.API;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ekom.Services;

public class RevalidateService
{
    private readonly Catalog _catalog;
    readonly ILogger<RevalidateService> _logger;
    public RevalidateService(Catalog catalog, ILogger<RevalidateService> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task RevalidateAsync(HeadlessConfig headlessConfig, Guid nodeKey, string contentType)
    {

        try
        {
            foreach (var apis in headlessConfig.ReValidateApis)
            {
                if (contentType == "ekmProduct")
                {
                    var product = _catalog.GetProduct(nodeKey, apis.Store);

                    if (product != null)
                    {
                        await RevalidateProduct(apis, product);
                    }

                }
                else if (contentType == "ekmCategory")
                {
                    var category = _catalog.GetCategory(nodeKey, apis.Store);

                    if (category != null)
                    {
                        await RevalidateCategory(apis, category);
                    }
                }
                else if (contentType == "ekmProductVariant")
                {
                    var variant = _catalog.GetVariant(nodeKey, apis.Store);

                    if (variant != null)
                    {
                        await RevalidateProduct(apis, variant.Product);
                    }
                }
                else if (contentType == "ekmProductVariantGroup")
                {
                    var variantGroup = _catalog.GetVariantGroup(nodeKey, apis.Store);

                    if (variantGroup != null)
                    {
                        await RevalidateProduct(apis, variantGroup.Product);
                    }
                }
            }
           
        } catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to revalidate. Key: {key} ContentType: {contentType}", nodeKey, contentType);
        }
    }

    private async Task RevalidateProduct(RevalidateApi api, IProduct product)
    {
        var urls = product.UrlsWithContext.Where(x => x.Store == api.Store).DistinctBy(x => x.Url).Select(x => x.Url);

        await Deliver(api, urls);
        
    }
    private async Task RevalidateCategory(RevalidateApi api, ICategory category)
    {
        var urls = category.UrlsWithContext.Where(x => x.Store == api.Store).DistinctBy(x => x.Url).Select(x => x.Url);

        await Deliver(api, urls);
    }

    private async Task Deliver(RevalidateApi revalidateConfig, IEnumerable<string> urls)
    {

        using var client = new HttpClient();

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var requestContent = JsonSerializer.Serialize(new { urls = string.Join(",", urls) });

        var url = $"{revalidateConfig.Url}?token={revalidateConfig.Secret}";

        var stringContent = new StringContent(requestContent, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, stringContent).ConfigureAwait(false);

        stringContent.Dispose();

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = $"Failed to post to revalidate API. URL: {url}, Status Code: {response.StatusCode} ReasonPhrase: {response.ReasonPhrase}";
            throw new HttpRequestException(errorMessage);
        }
    }

}
