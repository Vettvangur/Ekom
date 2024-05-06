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
            if (contentType == "ekmProduct")
            {
                var product = _catalog.GetProduct(nodeKey);

                if (product != null)
                {
                    await RevalidateProduct(headlessConfig, product);
                }

            }
            else if (contentType == "ekmCategory")
            {
                var category = _catalog.GetCategory(nodeKey);

                if (category != null)
                {
                    var productsResponse = category.ProductsRecursive();

                    foreach (var product in productsResponse.Products)
                    {
                        await RevalidateProduct(headlessConfig, product);
                    }
                }
            }
            else if (contentType == "ekmProductVariant")
            {
                var variant = _catalog.GetVariant(nodeKey);

                if (variant != null)
                {
                    await RevalidateProduct(headlessConfig, variant.Product);
                }
            }
            else if (contentType == "ekmProductVariantGroup")
            {
                var variantGroup = _catalog.GetVariantGroup(nodeKey);

                if (variantGroup != null)
                {
                    await RevalidateProduct(headlessConfig, variantGroup.Product);
                }
            }
        } catch(Exception ex)
        {
            _logger.LogError(ex, "Failed to revalidate. Key: {key} ContentType: {contentType}", nodeKey, contentType);
        }
    }

    private async Task RevalidateProduct(HeadlessConfig headlessConfig, IProduct product)
    {
        foreach (var urlsByStore in product.UrlsWithContext.GroupBy(x => x.Store))
        {
            var revalidateConfig = headlessConfig.ReValidateApis.FirstOrDefault(x => x.Store == urlsByStore.Key);

            if (revalidateConfig != null)
            {
                await Deliver(revalidateConfig, urlsByStore.Select(x => x.Url).DistinctBy(x => x).ToList());
            }
        }
    }


    private async Task Deliver(RevalidateApi revalidateConfig, List<string> urls)
    {
        using var client = new HttpClient();

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var requestContent = JsonSerializer.Serialize(new { urls });

        var url = $"{revalidateConfig.Url}?token={revalidateConfig.Secret}";

        var response = await client.PostAsync(url, new StringContent(requestContent, Encoding.UTF8, "application/json")).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = $"Failed to post to revalidate API. URL: {url}, Status Code: {response.StatusCode}";
            throw new HttpRequestException(errorMessage);
        }
    }

}
