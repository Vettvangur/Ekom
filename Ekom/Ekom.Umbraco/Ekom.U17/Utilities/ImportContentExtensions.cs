using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Ekom.Utilities;

internal static class ImportContentExtensions
{
    private static readonly string[] AllCultures = ["*"];

    public static void SetProperty(this IContent content, string alias, Dictionary<string, object> values, PropertyEditorType type = PropertyEditorType.Empty)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(alias);
        ArgumentNullException.ThrowIfNull(values);

        var property = content.Properties.FirstOrDefault(x => x.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
        if (property == null)
        {
            throw new InvalidOperationException("Unable to find matching property on IContent.");
        }

        var dataTypeService = Configuration.Resolver.GetService<IDataTypeService>();
        var dataType = dataTypeService?.GetDataType(property.PropertyType.DataTypeId);

        var value = JsonConvert.SerializeObject(new PropertyValue
        {
            DtdGuid = dataType?.Key ?? Guid.Empty,
            Values = values,
            Type = type == PropertyEditorType.Empty ? PropertyEditorType.Language : type,
        });

        content.SetValue(alias, value);
    }

    public static void SetSlug(this IContent content, Dictionary<string, object> values, PropertyEditorType type = PropertyEditorType.Empty)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(values);

        if (content.ContentType.Alias != "ekmProduct" && content.ContentType.Alias != "ekmCategory")
        {
            throw new ArgumentException("Slug can only be set on ekom product or category");
        }

        using var scope = Configuration.Resolver.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var umbracoService = scope.ServiceProvider.GetRequiredService<IUmbracoService>();
        var slugs = values.ToDictionary(x => x.Key, x => (object)umbracoService.UrlSegment(x.Value?.ToString() ?? string.Empty));

        content.SetProperty("slug", slugs, type);
    }

    public static void SetProperty(this IContent content, string alias, object? value)
    {
        content.SetValue(alias, value, null);
    }

    public static string GetProperty(this IContent content, string alias, string key)
    {
        var property = content.GetEkomProperty(alias);

        if (property?.Values != null && property.Values.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? string.Empty;
        }

        return string.Empty;
    }

    public static void SetPrice(this IContent content, string storeAlias, string currency, decimal price)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.HasProperty("price"))
        {
            return;
        }

        var fieldValue = content.GetValue<string>("price");
        var currencyPriceRoot = new CurrencyPriceRoot();

        try
        {
            currencyPriceRoot = string.IsNullOrEmpty(fieldValue)
                ? currencyPriceRoot
                : JsonConvert.DeserializeObject<CurrencyPriceRoot>(fieldValue) ?? currencyPriceRoot;

            if (!currencyPriceRoot.TryGetValue(storeAlias, out var storeItems))
            {
                currencyPriceRoot.Add(storeAlias, new List<CurrencyPrice> { new(price, currency) });
            }
            else
            {
                var priceObject = storeItems.FirstOrDefault(x => x.Currency == currency);
                if (priceObject == null)
                {
                    storeItems.Add(new CurrencyPrice(price, currency));
                }
                else
                {
                    priceObject.Price = price;
                }
            }

            content.SetValue("price", JsonConvert.SerializeObject(currencyPriceRoot));
            return;
        }
        catch
        {
            currencyPriceRoot = new CurrencyPriceRoot();
        }

        foreach (var store in API.Store.Instance.GetAllStores())
        {
            var currencyPrices = new List<CurrencyPrice>();

            if (!string.IsNullOrEmpty(fieldValue))
            {
                try
                {
                    var jsonCurrencyValue = fieldValue.GetEkomPropertyEditorValue(storeAlias);
                    currencyPrices = jsonCurrencyValue.GetCurrencyPrices();
                }
                catch
                {
                    currencyPrices = new List<CurrencyPrice>();
                }
            }

            if (storeAlias == store.Alias)
            {
                var priceObject = currencyPrices.FirstOrDefault(x => x.Currency == currency);
                if (priceObject == null)
                {
                    currencyPrices.Add(new CurrencyPrice(price, currency));
                }
                else
                {
                    priceObject.Price = price;
                }
            }

            currencyPriceRoot.Add(store.Alias, currencyPrices);
        }

        content.SetValue("price", JsonConvert.SerializeObject(currencyPriceRoot));
    }

    public static void SaveAndPublish(this IContentService contentService, IContent content, int userId = -1)
    {
        contentService.Save(content, userId: userId);
        contentService.Publish(content, AllCultures, userId: userId);
    }

    private static PropertyValue? GetEkomProperty(this IContent content, string alias)
    {
        ArgumentNullException.ThrowIfNull(content);

        var property = content.Properties.FirstOrDefault(x => x.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
        if (property?.GetValue() == null)
        {
            return null;
        }

        try
        {
            var propertyValue = property.GetValue()?.ToString();
            if (!string.IsNullOrEmpty(propertyValue) && !propertyValue.InvariantContains("values"))
            {
                propertyValue = "{\"values\":" + propertyValue + "}";
            }

            return JsonConvert.DeserializeObject<PropertyValue>(propertyValue ?? string.Empty);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
