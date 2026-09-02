using Ekom.Models;
using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Umb.Models;
using Ekom.Umb.VariantApp.Models;
using Ekom.Utilities;
using Newtonsoft.Json;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Ekom.Umb.VariantApp.Services;

internal sealed class VariantAppService : IVariantAppService
{
    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly INodeService _nodeService;
#if UMBRACO_18
    private readonly ILanguageService _languageService;
#else
    private readonly ILocalizationService _localizationService;
#endif
    private readonly VariantAppOptions _variantAppOptions;

    public VariantAppService(
        IContentService contentService,
        IContentTypeService contentTypeService,
        INodeService nodeService,
#if UMBRACO_18
        ILanguageService languageService,
#else
        ILocalizationService localizationService,
#endif
        IOptions<EkomOptions> options)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _nodeService = nodeService;
#if UMBRACO_18
        _languageService = languageService;
#else
        _localizationService = localizationService;
#endif
        _variantAppOptions = options.Value.VariantApp;
    }

    public VariantManagerProduct GetProductVariants(string productId)
    {
        var product = GetContent(productId, "ekmProduct");
        return MapProduct(product);
    }

    public VariantManagerCount GetVariantCount(string productId)
    {
        var product = GetContent(productId, "ekmProduct");
        return new VariantManagerCount
        {
            Count = CountVariants(product),
        };
    }

    public VariantManagerGroup CreateVariantGroup(VariantManagerGroupRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = GetContent(request.ProductId, "ekmProduct");
        var title = GetRequiredTitle(request.Title, "Variant group");
        var stores = LoadVariantStores(product);
        var languages = LoadVariantLanguages(stores);
        var group = _contentService.Create(title, product.Id, "ekmProductVariantGroup");

        group.SetProperty("title", CreateTitleValues(title, languages));
        group.SetValue("color", request.Color ?? string.Empty);
        group.SetValue("images", request.Images ?? string.Empty);
        SaveContent(group, request.Publish);

        return MapGroup(group, stores, languages);
    }

    public async Task<VariantManagerVariant> CreateVariantAsync(VariantManagerVariantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var group = GetContent(request.GroupId, "ekmProductVariantGroup");
        var title = GetRequiredTitle(request.Title, "Variant");
        var variant = _contentService.Create(title, group.Id, "ekmProductVariant");

        var product = GetContent(group.ParentId.ToString(CultureInfo.InvariantCulture), "ekmProduct");
        var stores = LoadVariantStores(product);
        var languages = LoadVariantLanguages(stores);

        ApplyVariantValues(variant, request.Title, request.Sku, request.Images, request.Price, request.Stock, stores, languages);
        SaveContent(variant, request.Publish);
        await ApplyStockValuesAsync(variant.Key, ParseStockValues(request.Stock)).ConfigureAwait(false);

        return MapVariant(variant, stores, languages);
    }

    public async Task<VariantManagerProduct> SaveProductVariantsAsync(VariantManagerSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = GetContent(request.ProductId, "ekmProduct");
        var stores = LoadVariantStores(product);
        var languages = LoadVariantLanguages(stores);
        var groupFieldDefinitions = GetVariantGroupFieldDefinitions();
        var variantFieldDefinitions = GetVariantFieldDefinitions();

        foreach (var groupModel in request.Groups)
        {
            var group = GetOrCreateVariantGroup(product, groupModel, languages);

            if (group.ParentId != product.Id)
            {
                continue;
            }

            if (groupModel.Changed || groupModel.Id <= 0)
            {
                ApplyGroupValues(group, groupModel, groupFieldDefinitions, languages);
                group.SortOrder = groupModel.SortOrder;
                SaveContent(group, request.Publish);
            }

            foreach (var variantModel in groupModel.Variants)
            {
                var isNewVariant = variantModel.Id <= 0;
                var variant = GetOrCreateVariant(group, variantModel, languages);

                if (variant.ParentId != group.Id)
                {
                    continue;
                }

                if (isNewVariant || HasVariantContentChanges(variant, variantModel, stores, variantFieldDefinitions, languages))
                {
                    ApplyVariantValues(variant, variantModel, stores, variantFieldDefinitions, languages);
                    variant.SortOrder = variantModel.SortOrder;
                    SaveContent(variant, request.Publish);
                }

                await ApplyStockValuesAsync(variant.Key, variantModel.StockValues).ConfigureAwait(false);
            }
        }

        return MapProduct(product);
    }

    public VariantManagerGroup SaveVariantGroup(VariantManagerGroupSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = GetContent(request.ProductId, "ekmProduct");
        var stores = LoadVariantStores(product);
        var languages = LoadVariantLanguages(stores);
        var group = GetOrCreateVariantGroup(product, request.Group, languages);

        if (group.ParentId != product.Id)
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.BadRequest);
        }

        ApplyGroupValues(group, request.Group, GetVariantGroupFieldDefinitions(), languages);
        SaveContent(group, request.Publish);

        return MapGroup(group, stores, languages);
    }

    public async Task<VariantManagerVariant> SaveVariantAsync(VariantManagerVariantSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var group = GetContent(request.GroupId, "ekmProductVariantGroup");
        var product = GetContent(group.ParentId.ToString(CultureInfo.InvariantCulture), "ekmProduct");
        var stores = LoadVariantStores(product);
        var languages = LoadVariantLanguages(stores);
        var variant = GetOrCreateVariant(group, request.Variant, languages);

        if (variant.ParentId != group.Id)
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.BadRequest);
        }

        var variantFieldDefinitions = GetVariantFieldDefinitions();
        if (request.Variant.Id <= 0 || HasVariantContentChanges(variant, request.Variant, stores, variantFieldDefinitions, languages))
        {
            ApplyVariantValues(variant, request.Variant, stores, variantFieldDefinitions, languages);
            SaveContent(variant, request.Publish);
        }

        await ApplyStockValuesAsync(variant.Key, request.Variant.StockValues).ConfigureAwait(false);

        return MapVariant(variant, stores, languages);
    }

    public bool DeleteVariantNode(string nodeId)
    {
        var content = GetContent(nodeId, null);

        if (content.ContentType.Alias is not ("ekmProductVariant" or "ekmProductVariantGroup"))
        {
            return false;
        }

        _contentService.MoveToRecycleBin(content);
        return true;
    }

    public string GetMediaThumbnailPath(string mediaId, int width, int height)
    {
        var media = _nodeService.MediaById(mediaId);

        if (media == null || string.IsNullOrWhiteSpace(media.Url) || media.Url == "#")
        {
            return string.Empty;
        }

        return AppendImageSize(media.Url, width, height);
    }

    private IContent GetContent(string id, string? expectedAlias)
    {
        IContent? content = null;

        if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intId))
        {
            content = _contentService.GetById(intId);
        }
        else if (Guid.TryParse(id, out var key))
        {
            content = _contentService.GetById(key);
        }

        if (content == null)
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.NotFound);
        }

        if (!string.IsNullOrWhiteSpace(expectedAlias) && !content.ContentType.Alias.Equals(expectedAlias, StringComparison.OrdinalIgnoreCase))
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.BadRequest);
        }

        return content;
    }

    private static string AppendImageSize(string url, int width, int height)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return string.Concat(url, separator, "width=", Math.Max(width, 1).ToString(CultureInfo.InvariantCulture), "&height=", Math.Max(height, 1).ToString(CultureInfo.InvariantCulture));
    }

    private VariantManagerProduct MapProduct(IContent product)
    {
        var stores = LoadVariantStores(product);
        var languages = LoadVariantLanguages(stores);
        var groupFieldDefinitions = GetVariantGroupFieldDefinitions();
        var variantFieldDefinitions = GetVariantFieldDefinitions();
        var groups = GetChildren(product.Id, "ekmProductVariantGroup")
            .Select(group => MapGroup(group, stores, languages, groupFieldDefinitions, variantFieldDefinitions))
            .ToList();

        return new VariantManagerProduct
        {
            Id = product.Id,
            Key = product.Key,
            Name = product.Name ?? string.Empty,
            Title = GetDisplayTitle(GetTitleValues(product, "title", languages), product.Name ?? string.Empty),
            Sku = GetStringValue(product, "sku"),
            VariantCount = groups.Sum(x => x.Variants.Count),
            Languages = languages,
            Stores = stores,
            VariantGroupFields = groupFieldDefinitions,
            VariantFields = variantFieldDefinitions,
            Groups = groups,
        };
    }

    private VariantManagerGroup MapGroup(IContent group, IReadOnlyList<VariantManagerStore> stores)
        => MapGroup(group, stores, LoadVariantLanguages(stores), GetVariantGroupFieldDefinitions(), GetVariantFieldDefinitions());

    private VariantManagerGroup MapGroup(IContent group, IReadOnlyList<VariantManagerStore> stores, IReadOnlyList<UmbracoLanguage> languages)
        => MapGroup(group, stores, languages, GetVariantGroupFieldDefinitions(), GetVariantFieldDefinitions());

    private VariantManagerGroup MapGroup(
        IContent group,
        IReadOnlyList<VariantManagerStore> stores,
        IReadOnlyList<UmbracoLanguage> languages,
        IReadOnlyList<VariantManagerCustomFieldDefinition> groupFieldDefinitions,
        IReadOnlyList<VariantManagerCustomFieldDefinition> variantFieldDefinitions)
    {
        var titleValues = GetTitleValues(group, "title", languages);

        return new VariantManagerGroup
        {
            Id = group.Id,
            Key = group.Key,
            Name = group.Name ?? string.Empty,
            Title = GetDisplayTitle(titleValues, group.Name ?? string.Empty),
            TitleValues = titleValues,
            Color = GetStringValue(group, "color"),
            Images = GetStringValue(group, "images"),
            SortOrder = group.SortOrder,
            Published = group.Published,
            CustomFields = GetCustomFields(group, groupFieldDefinitions),
            Variants = GetChildren(group.Id, "ekmProductVariant").Select(variant => MapVariant(variant, stores, languages, variantFieldDefinitions)).ToList(),
        };
    }

    private VariantManagerVariant MapVariant(IContent variant, IReadOnlyList<VariantManagerStore> stores)
        => MapVariant(variant, stores, LoadVariantLanguages(stores), GetVariantFieldDefinitions());

    private VariantManagerVariant MapVariant(IContent variant, IReadOnlyList<VariantManagerStore> stores, IReadOnlyList<UmbracoLanguage> languages)
        => MapVariant(variant, stores, languages, GetVariantFieldDefinitions());

    private VariantManagerVariant MapVariant(
        IContent variant,
        IReadOnlyList<VariantManagerStore> stores,
        IReadOnlyList<UmbracoLanguage> languages,
        IReadOnlyList<VariantManagerCustomFieldDefinition> fieldDefinitions)
    {
        var titleValues = GetTitleValues(variant, "title", languages);

        return new VariantManagerVariant
        {
            Id = variant.Id,
            Key = variant.Key,
            Name = variant.Name ?? string.Empty,
            Title = GetDisplayTitle(titleValues, variant.Name ?? string.Empty),
            TitleValues = titleValues,
            Sku = GetStringValue(variant, "sku"),
            Images = GetStringValue(variant, "images"),
            PriceValues = GetPriceValues(variant, stores),
            StockValues = GetStockValues(variant),
            CustomFields = GetCustomFields(variant, fieldDefinitions),
            SortOrder = variant.SortOrder,
            Published = variant.Published,
        };
    }

    private IReadOnlyList<IContent> GetChildren(int parentId, string contentTypeAlias)
    {
        return _contentService.GetPagedChildren(parentId, 0, int.MaxValue, out _)
            .Where(x => !x.Trashed && x.ContentType.Alias.Equals(contentTypeAlias, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private int CountVariants(IContent product)
    {
        return GetChildren(product.Id, "ekmProductVariantGroup")
            .Sum(group => GetChildren(group.Id, "ekmProductVariant").Count);
    }

    private IContent GetOrCreateVariantGroup(IContent product, VariantManagerGroup groupModel, IReadOnlyList<UmbracoLanguage> languages)
    {
        if (groupModel.Id > 0)
        {
            return GetContent(groupModel.Id.ToString(CultureInfo.InvariantCulture), "ekmProductVariantGroup");
        }

        var title = GetDisplayTitle(NormalizeTitleValues(groupModel.TitleValues, groupModel.Title, "Variant group", languages), "Variant group");
        return _contentService.Create(title, product.Id, "ekmProductVariantGroup");
    }

    private IContent GetOrCreateVariant(IContent group, VariantManagerVariant variantModel, IReadOnlyList<UmbracoLanguage> languages)
    {
        if (variantModel.Id > 0)
        {
            return GetContent(variantModel.Id.ToString(CultureInfo.InvariantCulture), "ekmProductVariant");
        }

        var title = GetDisplayTitle(NormalizeTitleValues(variantModel.TitleValues, variantModel.Title, "Variant", languages), "Variant");
        return _contentService.Create(title, group.Id, "ekmProductVariant");
    }

    private static void ApplyVariantValues(
        IContent variant,
        VariantManagerVariant variantModel,
        IReadOnlyList<VariantManagerStore> stores,
        IReadOnlyList<VariantManagerCustomFieldDefinition> fieldDefinitions,
        IReadOnlyList<UmbracoLanguage> languages)
    {
        var variantTitleValues = NormalizeTitleValues(variantModel.TitleValues, variantModel.Title, variant.Name ?? "Variant", languages);
        var variantTitle = GetDisplayTitle(variantTitleValues, variant.Name ?? "Variant");
        variant.Name = variantTitle;
        variant.SetProperty("title", ToObjectDictionary(variantTitleValues));
        variant.SetValue("sku", variantModel.Sku ?? string.Empty);
        variant.SetValue("images", variantModel.Images ?? string.Empty);
        ApplyCustomFields(variant, variantModel.CustomFields, fieldDefinitions);
        ApplyPriceValues(variant, variantModel.PriceValues, stores);
    }

    private static void ApplyPriceValues(IContent variant, CurrencyPriceRoot? priceValues, IReadOnlyList<VariantManagerStore> stores)
    {
        variant.SetValue("price", JsonConvert.SerializeObject(NormalizePriceValues(GetPriceValues(variant, stores), stores)));

        foreach (var priceValue in NormalizePriceValues(priceValues, stores))
        {
            foreach (var price in priceValue.Value)
            {
                variant.SetPrice(priceValue.Key, price.Currency, price.Price ?? 0);
            }
        }
    }

    private bool HasVariantContentChanges(
        IContent variant,
        VariantManagerVariant variantModel,
        IReadOnlyList<VariantManagerStore> stores,
        IReadOnlyList<VariantManagerCustomFieldDefinition> fieldDefinitions,
        IReadOnlyList<UmbracoLanguage> languages)
    {
        var titleValues = NormalizeTitleValues(variantModel.TitleValues, variantModel.Title, variant.Name ?? "Variant", languages);

        return !DictionariesEqual(GetTitleValues(variant, "title", languages), titleValues)
            || !string.Equals(GetStringValue(variant, "sku"), variantModel.Sku ?? string.Empty, StringComparison.Ordinal)
            || !string.Equals(GetStringValue(variant, "images"), variantModel.Images ?? string.Empty, StringComparison.Ordinal)
            || variant.SortOrder != variantModel.SortOrder
            || !CustomFieldsEqual(variant, variantModel.CustomFields, fieldDefinitions)
            || !PriceValuesEqual(GetPriceValues(variant, stores), variantModel.PriceValues, stores);
    }

    private static bool DictionariesEqual(IDictionary<string, string> left, IDictionary<string, string> right)
    {
        return left.Count == right.Count
            && left.All(x => right.TryGetValue(x.Key, out var value) && string.Equals(x.Value, value, StringComparison.Ordinal));
    }

    private static bool PriceValuesEqual(CurrencyPriceRoot left, CurrencyPriceRoot? right, IReadOnlyList<VariantManagerStore> stores)
    {
        return JsonConvert.SerializeObject(NormalizePriceValues(left, stores)) == JsonConvert.SerializeObject(NormalizePriceValues(right, stores));
    }

    private static async Task ApplyStockValuesAsync(Guid key, IReadOnlyList<StockRequest>? stockValues)
    {
        foreach (var stock in stockValues ?? Array.Empty<StockRequest>())
        {
            if (string.IsNullOrWhiteSpace(stock.StoreAlias))
            {
                await API.Stock.Instance.SetStockAsync(key, stock.Value ?? 0).ConfigureAwait(false);
            }
            else
            {
                await API.Stock.Instance.SetStockAsync(key, stock.StoreAlias, stock.Value ?? 0).ConfigureAwait(false);
            }
        }
    }

    private static void ApplyGroupValues(
        IContent group,
        VariantManagerGroup groupModel,
        IReadOnlyList<VariantManagerCustomFieldDefinition> fieldDefinitions,
        IReadOnlyList<UmbracoLanguage> languages)
    {
        var groupTitleValues = NormalizeTitleValues(groupModel.TitleValues, groupModel.Title, group.Name ?? "Variant group", languages);
        var groupTitle = GetDisplayTitle(groupTitleValues, group.Name ?? "Variant group");
        group.Name = groupTitle;
        group.SetProperty("title", ToObjectDictionary(groupTitleValues));
        group.SetValue("color", groupModel.Color ?? string.Empty);
        group.SetValue("images", groupModel.Images ?? string.Empty);
        ApplyCustomFields(group, groupModel.CustomFields, fieldDefinitions);
    }

    private void ApplyVariantValues(
        IContent variant,
        string title,
        string sku,
        string images,
        string price,
        string stock,
        IReadOnlyList<VariantManagerStore> stores,
        IReadOnlyList<UmbracoLanguage> languages)
    {
        ApplyVariantValues(variant, new VariantManagerVariant
        {
            Title = title,
            TitleValues = CreateTitleValues(title, languages).ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty),
            Sku = sku,
            Images = images,
            PriceValues = ParsePriceValues(price),
            StockValues = ParseStockValues(stock),
        }, stores, Array.Empty<VariantManagerCustomFieldDefinition>(), languages);
    }

    private IReadOnlyList<VariantManagerCustomFieldDefinition> GetVariantGroupFieldDefinitions()
        => GetCustomFieldDefinitions("ekmProductVariantGroup", _variantAppOptions.VariantGroups);

    private IReadOnlyList<VariantManagerCustomFieldDefinition> GetVariantFieldDefinitions()
        => GetCustomFieldDefinitions("ekmProductVariant", _variantAppOptions.Variants);

    private IReadOnlyList<VariantManagerCustomFieldDefinition> GetCustomFieldDefinitions(string contentTypeAlias, IReadOnlyList<string> aliases)
    {
        if (aliases.Count == 0)
        {
            return Array.Empty<VariantManagerCustomFieldDefinition>();
        }

        var contentType = _contentTypeService.Get(contentTypeAlias);
        if (contentType == null)
        {
            return Array.Empty<VariantManagerCustomFieldDefinition>();
        }

        var definitions = new List<VariantManagerCustomFieldDefinition>();
        foreach (var alias in aliases.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var property = contentType.CompositionPropertyTypes.FirstOrDefault(x => x.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
            if (property == null || !IsTextStringProperty(property))
            {
                continue;
            }

            definitions.Add(new VariantManagerCustomFieldDefinition
            {
                Alias = property.Alias,
                Label = property.Name ?? property.Alias,
                Required = property.Mandatory,
            });
        }

        return definitions;
    }

    private static bool IsTextStringProperty(IPropertyType property)
        => string.Equals(property.PropertyEditorAlias, "Umbraco.TextBox", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<VariantManagerCustomField> GetCustomFields(IContent content, IReadOnlyList<VariantManagerCustomFieldDefinition> fieldDefinitions)
        => fieldDefinitions
            .Select(field => new VariantManagerCustomField
            {
                Alias = field.Alias,
                Label = field.Label,
                Required = field.Required,
                Value = content.GetValue<string>(field.Alias) ?? string.Empty,
            })
            .ToList();

    private static void ApplyCustomFields(IContent content, IReadOnlyList<VariantManagerCustomField>? customFields, IReadOnlyList<VariantManagerCustomFieldDefinition> fieldDefinitions)
    {
        var values = (customFields ?? Array.Empty<VariantManagerCustomField>())
            .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        foreach (var field in fieldDefinitions)
        {
            values.TryGetValue(field.Alias, out var value);
            value ??= string.Empty;

            if (field.Required && string.IsNullOrWhiteSpace(value))
            {
                throw new Exceptions.HttpResponseException(HttpStatusCode.BadRequest);
            }

            content.SetValue(field.Alias, value);
        }
    }

    private static bool CustomFieldsEqual(IContent content, IReadOnlyList<VariantManagerCustomField>? customFields, IReadOnlyList<VariantManagerCustomFieldDefinition> fieldDefinitions)
    {
        var values = (customFields ?? Array.Empty<VariantManagerCustomField>())
            .GroupBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        return fieldDefinitions.All(field => values.TryGetValue(field.Alias, out var value)
            && string.Equals(content.GetValue<string>(field.Alias) ?? string.Empty, value ?? string.Empty, StringComparison.Ordinal));
    }

    private IReadOnlyList<VariantManagerStore> LoadVariantStores(IContent product)
    {
        var enabledStores = GetEnabledStores(product).ToList();

        return enabledStores
            .Select(store => new VariantManagerStore
            {
                Alias = store.Alias,
                Title = store.Title,
                Currencies = store.Currencies.ToList(),
            })
            .ToList();
    }

    private IEnumerable<IStore> GetEnabledStores(IContent product)
    {
        var allStores = API.Store.Instance.GetAllStores().ToList();
        var node = _nodeService.NodeById(product.Id, true);

        if (node == null)
        {
            return allStores;
        }

        var ancestors = _nodeService.GetAllCatalogAncestors(node);
        var stores = new List<IStore>();

        foreach (var store in allStores)
        {
            var alias = store.Alias;

            if (node.Properties.GetValue("disable", alias).IsBoolean())
            {
                continue;
            }

            if (ancestors.Any(ancestor => ancestor.Properties.GetValue("disable", alias).IsBoolean()))
            {
                continue;
            }

            stores.Add(store);
        }

        return stores;
    }

    private IReadOnlyList<UmbracoLanguage> LoadVariantLanguages(IReadOnlyList<VariantManagerStore> stores)
    {
        var languages = LoadLanguages();
        var supportedCultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var storeAliases = stores.Select(x => x.Alias).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var store in API.Store.Instance.GetAllStores().Where(store => storeAliases.Contains(store.Alias)))
        {
            foreach (var culture in store.Cultures)
            {
                if (!string.IsNullOrWhiteSpace(culture.Name))
                {
                    supportedCultures.Add(culture.Name);
                }
            }
        }

        if (supportedCultures.Count == 0)
        {
            return languages;
        }

        return languages.Where(language => supportedCultures.Contains(language.IsoCode)).ToList();
    }

    private static IDictionary<string, string> GetTitleValues(IContent content, string alias, IReadOnlyList<UmbracoLanguage> languages)
    {
        var property = ParsePropertyValue(content.GetValue<string>(alias));
        IDictionary<string, string> values;

        if (property?.Values == null)
        {
            var value = content.GetValue<string>(alias) ?? string.Empty;
            values = string.IsNullOrWhiteSpace(value)
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [string.Empty] = value };
        }
        else
        {
            values = property.Values.ToDictionary(x => x.Key, x => x.Value?.ToString() ?? string.Empty);
        }

        var displayValue = GetDisplayTitle(values, string.Empty);
        if (!string.IsNullOrWhiteSpace(displayValue))
        {
            foreach (var language in languages)
            {
                values.TryAdd(language.IsoCode, displayValue);
            }
        }

        if (languages.Count > 0)
        {
            var languageCodes = languages.Select(x => x.IsoCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            values = values
                .Where(x => languageCodes.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
        }

        return values;
    }

    private static PropertyValue? ParsePropertyValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var propertyValue = value.InvariantContains("values") ? value : "{\"values\":" + value + "}";
            return JsonConvert.DeserializeObject<PropertyValue>(propertyValue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GetDisplayTitle(IDictionary<string, string> values, string fallback)
    {
        return values.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? fallback;
    }

    private static Dictionary<string, object> CreateTitleValues(string title, IReadOnlyList<UmbracoLanguage> languages)
    {
        return languages.Count == 0
            ? new Dictionary<string, object> { [string.Empty] = title }
            : languages.ToDictionary(x => x.IsoCode, _ => (object)title);
    }

    private static IDictionary<string, string> NormalizeTitleValues(IDictionary<string, string>? values, string? title, string fallback, IReadOnlyList<UmbracoLanguage> languages)
    {
        if (values != null && values.Any(x => !string.IsNullOrWhiteSpace(x.Value)))
        {
            var normalized = values.ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            if (languages.Count > 0)
            {
                var languageCodes = languages.Select(x => x.IsoCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                normalized = normalized
                    .Where(x => languageCodes.Contains(x.Key))
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            }

            if (normalized.Any(x => !string.IsNullOrWhiteSpace(x.Value)))
            {
                return normalized;
            }
        }

        var requiredTitle = GetRequiredTitle(title, fallback);
        return languages.Count == 0
            ? new Dictionary<string, string> { [string.Empty] = requiredTitle }
            : languages.ToDictionary(x => x.IsoCode, _ => requiredTitle, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object> ToObjectDictionary(IDictionary<string, string> values)
    {
        return values.ToDictionary(x => x.Key, x => (object)x.Value);
    }

    private static CurrencyPriceRoot GetPriceValues(IContent content, IReadOnlyList<VariantManagerStore> stores)
    {
        var item = new Umbraco17Content(content, Guid.Empty);
        var root = new CurrencyPriceRoot();

        foreach (var store in stores)
        {
            var prices = new List<CurrencyPrice>();

            foreach (var currency in store.Currencies)
            {
                if (string.IsNullOrWhiteSpace(currency.CurrencyValue))
                {
                    continue;
                }

                prices.Add(new CurrencyPrice(item.GetPrice(store.Alias, currency.CurrencyValue), currency.CurrencyValue));
            }

            if (prices.Count > 0)
            {
                root[store.Alias] = prices;
            }
        }

        return root;
    }

    private static CurrencyPriceRoot ParsePriceValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new CurrencyPriceRoot();
        }

        try
        {
            return JsonConvert.DeserializeObject<CurrencyPriceRoot>(value) ?? new CurrencyPriceRoot();
        }
        catch (JsonException)
        {
            return new CurrencyPriceRoot();
        }
    }

    private static CurrencyPriceRoot NormalizePriceValues(CurrencyPriceRoot? values, IReadOnlyList<VariantManagerStore> stores)
    {
        if (values == null || values.Count == 0)
        {
            return new CurrencyPriceRoot();
        }

        var storeAliases = stores
            .Select(x => x.Alias)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        var normalized = new CurrencyPriceRoot();

        foreach (var storeAlias in storeAliases)
        {
            var prices = new Dictionary<string, CurrencyPrice>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in values.Where(x => x.Key.Equals(storeAlias, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var price in pair.Value.Where(x => !string.IsNullOrWhiteSpace(x.Currency)))
                {
                    prices[price.Currency] = new CurrencyPrice(price.Price ?? 0, price.Currency);
                }
            }

            if (prices.Count > 0)
            {
                normalized[storeAlias] = prices.Values.ToList();
            }
        }

        return normalized;
    }

    private static IReadOnlyList<StockRequest> GetStockValues(IContent content)
    {
        var stores = API.Store.Instance.GetAllStores().ToList();

        if (Configuration.Instance.PerStoreStock && stores.Count > 1)
        {
            return stores
                .Select(store => new StockRequest
                {
                    StoreAlias = store.Alias,
                    Value = API.Stock.Instance.GetStock(content.Key, store.Alias),
                })
                .ToList();
        }

        return new[]
        {
            new StockRequest
            {
                StoreAlias = string.Empty,
                Value = API.Stock.Instance.GetStock(content.Key),
            },
        };
    }

    private static IReadOnlyList<StockRequest> ParseStockValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<StockRequest>();
        }

        try
        {
            return JsonConvert.DeserializeObject<IReadOnlyList<StockRequest>>(value) ?? Array.Empty<StockRequest>();
        }
        catch (JsonException)
        {
            return Array.Empty<StockRequest>();
        }
    }

    private void SaveContent(IContent content, bool publish)
    {
        if (publish)
        {
            _contentService.Save(content);
            _contentService.Publish(content, ["*"]);
            return;
        }

        _contentService.Save(content);
    }

    private static string GetStringValue(IContent content, string alias)
    {
        return content.GetValue<string>(alias) ?? string.Empty;
    }

    private static string GetRequiredTitle(string? title, string fallback)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.BadRequest);
        }

        return title.Trim();
    }

    private IReadOnlyList<UmbracoLanguage> LoadLanguages()
    {
        return GetAllLanguages()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CultureName)
            .Select(x => new UmbracoLanguage
            {
                Culture = x.CultureInfo ?? CultureInfo.InvariantCulture,
                CultureName = x.CultureName ?? string.Empty,
                IsoCode = x.IsoCode,
            })
            .ToList();
    }

    private IEnumerable<ILanguage> GetAllLanguages()
    {
#if UMBRACO_18
        return _languageService.GetAllAsync().GetAwaiter().GetResult();
#else
        return _localizationService.GetAllLanguages();
#endif
    }

}
