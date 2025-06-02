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
            if (contentType == "ekmProduct" || contentType == "ekmCategory" || contentType == "ekmProductVariant" || contentType == "ekmProductVariantGroup")
            {
                foreach (RevalidateApi apis in headlessConfig.ReValidateApis)
                {
                    if (contentType == "ekmProduct")
                    {
                        IProduct? product = _catalog.GetProduct(nodeKey, apis.Store, false);

                        if (product != null)
                        {
                            await RevalidateProduct(apis, product);
                        }

                    }
                    else if (contentType == "ekmCategory")
                    {
                        ICategory? category = _catalog.GetCategory(nodeKey, apis.Store, false);

                        if (category != null)
                        {
                            await RevalidateCategory(apis, category);
                        }
                    }
                    else if (contentType == "ekmProductVariant")
                    {
                        IVariant? variant = _catalog.GetVariant(nodeKey, apis.Store);

                        if (variant != null && variant.Product != null)
                        {
                            await RevalidateProduct(apis, variant.Product);
                        }
                    }
                    else if (contentType == "ekmProductVariantGroup")
                    {
                        IVariantGroup? variantGroup = _catalog.GetVariantGroup(nodeKey, apis.Store);

                        if (variantGroup != null)
                        {
                            await RevalidateProduct(apis, variantGroup.Product);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revalidate. Key: {key} ContentType: {contentType}", nodeKey, contentType);
        }
    }

    private async Task RevalidateProduct(RevalidateApi api, IProduct product)
    {
        IEnumerable<string> urls = product.UrlsWithContext.Where(x => x.Store == api.Store).DistinctBy(x => x.Url).Select(x => x.Url);

        await Deliver(api, urls);

    }
    private async Task RevalidateCategory(RevalidateApi api, ICategory category)
    {
        IEnumerable<string> urls = category.UrlsWithContext.Where(x => x.Store == api.Store).DistinctBy(x => x.Url).Select(x => x.Url);

        await Deliver(api, urls);
    }

    private async Task Deliver(RevalidateApi revalidateConfig, IEnumerable<string> urls)
    {
        if (!urls.Any())
        {
            return;
        }

        using HttpClient client = new HttpClient();

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        string requestContent = JsonSerializer.Serialize(new { urls = string.Join(",", urls) });

        string url = $"{revalidateConfig.Url}?token={revalidateConfig.Secret}";

        StringContent stringContent = new StringContent(requestContent, Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(url, stringContent).ConfigureAwait(false);

        stringContent.Dispose();

        if (!response.IsSuccessStatusCode)
        {
            string errorMessage = $"Failed to post to revalidate API. URL: {url}, Status Code: {response.StatusCode} ReasonPhrase: {response.ReasonPhrase}";

            _logger.LogError(errorMessage, response.ReasonPhrase);
        }
    }

}
