using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;
using JsonElement = System.Text.Json.JsonElement;
using JsonValueKind = System.Text.Json.JsonValueKind;

namespace Ekom.Utilities;

public static class ImportContentExtensions
{
    private static readonly string[] AllCultures = ["*"];
    private const string RichTextEditorAlias = "Umbraco.RichText";
    private const string LegacyRichTextEditorAlias = "Umbraco.TinyMCE";

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
#if UMBRACO_18
        var idKeyMap = Configuration.Resolver.GetService<IIdKeyMap>();
        var dataType = dataTypeService != null && idKeyMap != null
            ? property.PropertyType.GetDataType(dataTypeService, idKeyMap)
            : null;
#else
        var dataType = dataTypeService?.GetDataType(property.PropertyType.DataTypeId);
#endif
        var propertyValues = IsRichTextEditor(dataType)
            ? values.ToDictionary(x => x.Key, x => CreateRichTextValue(x.Value))
            : values;

        var value = JsonConvert.SerializeObject(new PropertyValue
        {
            DtdGuid = dataType?.Key ?? Guid.Empty,
            Values = propertyValues,
            Type = GetPropertyEditorType(dataType, type),
        });

        content.SetValue(alias, value);
    }

    public static void SetAdditionalProperty(this IContent content, string alias, object? value)
    {
        var property = content.Properties.FirstOrDefault(x => x.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));

        if (value is Dictionary<string, object> values
            && string.Equals(property?.PropertyType.PropertyEditorAlias, "Ekom.Property", StringComparison.Ordinal))
        {
            content.SetProperty(alias, values);
            return;
        }

        content.SetValue(alias, value);
    }

    private static PropertyEditorType GetPropertyEditorType(IDataType? dataType, PropertyEditorType type)
    {
        if (type != PropertyEditorType.Empty)
        {
            return type;
        }

        if (dataType?.ConfigurationData?.TryGetValue("useLanguages", out var useLanguages) == true
            && bool.TryParse(useLanguages?.ToString(), out var value))
        {
            return value ? PropertyEditorType.Language : PropertyEditorType.Store;
        }

        return PropertyEditorType.Language;
    }

    private static bool IsRichTextEditor(IDataType? dataType)
    {
        if (dataType?.ConfigurationData == null)
        {
            return false;
        }

        if (!dataType.ConfigurationData.TryGetValue("dataType", out var wrappedDataType))
        {
            return false;
        }

        var propertyEditorAlias = GetPropertyEditorAlias(wrappedDataType);
        return string.Equals(propertyEditorAlias, RichTextEditorAlias, StringComparison.Ordinal)
            || string.Equals(propertyEditorAlias, LegacyRichTextEditorAlias, StringComparison.Ordinal);
    }

    private static object CreateRichTextValue(object? value)
    {
        if (TryParseRichTextValue(value, out var richTextValue))
        {
            return richTextValue;
        }

        return new { markup = value?.ToString() ?? string.Empty };
    }

    private static string? GetPropertyEditorAlias(object? value)
    {
        return value switch
        {
            JsonObject jsonObject => jsonObject["propertyEditorAlias"]?.GetValue<string>(),
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object &&
                jsonElement.TryGetProperty("propertyEditorAlias", out var propertyEditorAlias) &&
                propertyEditorAlias.ValueKind == JsonValueKind.String => propertyEditorAlias.GetString(),
            IDictionary<string, object> values when values.TryGetValue("propertyEditorAlias", out var propertyEditorAlias) => propertyEditorAlias?.ToString(),
            _ => null,
        };
    }

    private static bool TryParseRichTextValue(object? value, out JObject richTextValue)
    {
        richTextValue = new JObject();

        var json = value switch
        {
            JObject jsonObject => jsonObject.ToString(),
            JsonObject jsonObject => jsonObject.ToJsonString(),
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object => jsonElement.GetRawText(),
            string stringValue => stringValue,
            _ => null,
        };

        return json != null && TryParseRichTextValue(json, out richTextValue);
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

    /// <summary>Copy overrides to node JSON before disabling database prices. Caller saves/publishes explicitly.</summary>
    public static void CopyDatabasePricesToNode(this IContent content, string propertyAlias = "price")
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.HasProperty(propertyAlias)) throw new ArgumentException("Unknown price property.", nameof(propertyAlias));
        var value = PriceHelper.Export(content.GetValue<string>(propertyAlias),
            API.Prices.Instance.Get(content.Key, propertyAlias), API.Store.Instance.GetAllStores().ToList());
        content.SetValue(propertyAlias, value);
    }

    public static Task SetPriceAsync(this IContent content, string storeAlias, string currency, decimal price,
        string propertyAlias = "price", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyAlias);
        if (!content.HasProperty(propertyAlias)) return Task.CompletedTask;
        if (Configuration.Instance.UseDatabasePrices)
            return API.Prices.Instance.SetPriceAsync(content.Key, storeAlias, currency, price, propertyAlias, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        SetPrice(content, propertyAlias, storeAlias, currency, price);
        return Task.CompletedTask;
    }

    public static void SetPrice(this IContent content, string storeAlias, string currency, decimal price)
        => SetPrice(content, "price", storeAlias, currency, price);

    public static void SetPrice(this IContent content, string propertyAlias, string storeAlias, string currency, decimal price)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrEmpty(propertyAlias);

        if (!content.HasProperty(propertyAlias)) return;

        if (Configuration.Instance.UseDatabasePrices)
        {
            API.Prices.Instance.SetPrice(content.Key, storeAlias, currency, price, propertyAlias);
            return;
        }

        if (!content.HasProperty(propertyAlias))
        {
            return;
        }

        var fieldValue = content.GetValue<string>(propertyAlias);
        try
        {
            content.SetValue(propertyAlias, PriceHelper.SetPrice(fieldValue, price, currency, storeAlias));
            return;
        }
        catch
        {
        }

        var currencyPriceRoot = new CurrencyPriceRoot();

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

        content.SetValue(propertyAlias, JsonConvert.SerializeObject(currencyPriceRoot));
    }

    // Umbraco 18.1 added a save-and-publish that runs in one scope. Ekom also supports 18.0 and 17,
    // where it does not exist, so bind to it at runtime instead of raising the minimum version.
    private static readonly Func<IContentService, IContent, string[], int, PublishResult>? NativeSaveAndPublish
        = FindNativeSaveAndPublish();

    private static Func<IContentService, IContent, string[], int, PublishResult>? FindNativeSaveAndPublish()
    {
        foreach (var type in typeof(IContentService).GetInterfaces().Append(typeof(IContentService)))
        {
            var method = type.GetMethod("SaveAndPublish", [typeof(IContent), typeof(string[]), typeof(int)]);
            if (method != null && method.ReturnType == typeof(PublishResult))
            {
                return method.CreateDelegate<Func<IContentService, IContent, string[], int, PublishResult>>();
            }
        }

        return null;
    }

    public static void SaveAndPublish(this IContentService contentService, IContent content, int userId = -1)
    {
        // The native method takes explicit cultures only, and none for invariant content.
        if (NativeSaveAndPublish != null && !content.ContentType.VariesByCulture())
        {
            NativeSaveAndPublish(contentService, content, Array.Empty<string>(), userId);
            return;
        }

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
