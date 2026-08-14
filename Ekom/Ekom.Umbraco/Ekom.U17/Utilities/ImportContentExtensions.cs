using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Ekom.Utilities;

public static class ImportContentExtensions
{
    private static readonly string[] AllCultures = ["*"];
    private const string RichTextEditorAlias = "Umbraco.RichText";

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
        var propertyValues = IsRichTextEditor(dataType)
            ? values.ToDictionary(x => x.Key, x => CreateRichTextValue(x.Value))
            : values;

        var value = JsonConvert.SerializeObject(new PropertyValue
        {
            DtdGuid = dataType?.Key ?? Guid.Empty,
            Values = propertyValues,
            Type = type == PropertyEditorType.Empty ? PropertyEditorType.Language : type,
        });

        content.SetValue(alias, value);
    }

    private static bool IsRichTextEditor(IDataType? dataType)
    {
        if (dataType?.ConfigurationData == null)
        {
            return false;
        }

        var configuration = JObject.FromObject(dataType.ConfigurationData);
        return configuration["dataType"]?["propertyEditorAlias"]?.Value<string>() == RichTextEditorAlias;
    }

    private static object CreateRichTextValue(object? value)
    {
        if (value is JObject richTextValue && richTextValue["markup"] != null)
        {
            return richTextValue;
        }

        if (value is string stringValue && TryParseRichTextValue(stringValue, out richTextValue))
        {
            return richTextValue;
        }

        return new { markup = value?.ToString() ?? string.Empty };
    }

    private static bool TryParseRichTextValue(string value, out JObject richTextValue)
    {
        richTextValue = new JObject();

        try
        {
            richTextValue = JObject.Parse(value);
            return richTextValue["markup"] != null;
        }
        catch (JsonReaderException)
        {
            return false;
        }
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

            var storeItems = currencyPriceRoot
                .Where(x => x.Key.Equals(storeAlias, StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.Value)
                .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Last())
                .ToList();

            foreach (var key in currencyPriceRoot.Keys.Where(x => x.Equals(storeAlias, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                currencyPriceRoot.Remove(key);
            }

            if (storeItems.Count == 0)
            {
                storeItems.Add(new CurrencyPrice(price, currency));
                currencyPriceRoot.Add(storeAlias, storeItems);
            }
            else
            {
                var priceObject = storeItems.FirstOrDefault(x => x.Currency.Equals(currency, StringComparison.OrdinalIgnoreCase));
                if (priceObject == null)
                {
                    storeItems.Add(new CurrencyPrice(price, currency));
                }
                else
                {
                    priceObject.Price = price;
                }

                currencyPriceRoot.Add(storeAlias, storeItems);
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
