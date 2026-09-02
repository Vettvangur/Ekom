using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Umb.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

internal sealed class UmbracoService : IUmbracoService
{
    private const string LanguagesCacheKey = "ekmLanguages";
    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<UmbracoLanguage>>> LanguageLocks = new();

    private readonly IDataTypeService _dataTypeService;
    private readonly IDomainService _domainService;
    private readonly INodeService _nodeService;
#if UMBRACO_18
    private readonly ILanguageService _languageService;
    private readonly IIdKeyMap _idKeyMap;
#else
    private readonly ILocalizationService _localizationService;
#endif
    private readonly PropertyEditorCollection _propertyEditorCollection;
    private readonly IContentTypeService _contentTypeService;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IMemoryCache _cache;

    public UmbracoService(
        IDomainService domainService,
        IDataTypeService dataTypeService,
#if UMBRACO_18
        ILanguageService languageService,
        IIdKeyMap idKeyMap,
#else
        ILocalizationService localizationService,
#endif
        PropertyEditorCollection propertyEditorCollection,
        IContentTypeService contentTypeService,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        INodeService nodeService,
        IMemoryCache cache)
    {
        _domainService = domainService;
        _dataTypeService = dataTypeService;
#if UMBRACO_18
        _languageService = languageService;
        _idKeyMap = idKeyMap;
#else
        _localizationService = localizationService;
#endif
        _propertyEditorCollection = propertyEditorCollection;
        _contentTypeService = contentTypeService;
        _runtimeCache = appCaches.RuntimeCache;
        _shortStringHelper = shortStringHelper;
        _nodeService = nodeService;
        _cache = cache;
    }

    public IEnumerable<Ekom.Models.UmbracoDomain> GetDomains(bool includeWildcards = false)
    {
        return (_domainService.GetAllAsync(includeWildcards).GetAwaiter().GetResult()).Select(x => new Umbraco17Domain(x));
    }

    public string GetDictionaryValue(string key) => string.Empty;

    public string GetDataType(string typeValue)
    {
        if (int.TryParse(typeValue, out var typeValueInt))
        {
            var dataType = GetDataTypeCached(typeValueInt);

            if (dataType == null)
            {
                return string.Empty;
            }

            typeValue = dataType.ConfigurationAs<string>();
        }

        return typeValue.Contains('[', StringComparison.Ordinal)
            ? JsonConvert.DeserializeObject<string[]>(typeValue)?.FirstOrDefault() ?? string.Empty
            : typeValue;
    }

    public IEnumerable<string> GetContent(string guid)
    {
        var nodes = guid?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => _nodeService.NodeById(GuidUdiHelper.GetGuid(x)))
            .Where(x => x != null)
            .ToList();

        return nodes?.Select(x => x!.Id.ToString(CultureInfo.InvariantCulture)) ?? Array.Empty<string>();
    }

    public IEnumerable<UmbracoLanguage> GetLanguages()
    {
        return _runtimeCache.GetCacheItem(LanguagesCacheKey, () =>
        {
            var lazy = LanguageLocks.GetOrAdd(LanguagesCacheKey, _ =>
                new Lazy<IReadOnlyList<UmbracoLanguage>>(LoadLanguages, LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                return lazy.Value;
            }
            catch
            {
                LanguageLocks.TryRemove(LanguagesCacheKey, out _);
                throw;
            }
            finally
            {
                if (lazy.IsValueCreated)
                {
                    LanguageLocks.TryRemove(LanguagesCacheKey, out _);
                }
            }
        }, TimeSpan.FromHours(6)) ?? Array.Empty<UmbracoLanguage>();
    }

    public string DefaultLanguage()
    {
        return _runtimeCache.GetCacheItem(
            "ekmDefaultLanguage",
            GetDefaultLanguageIsoCode,
            TimeSpan.FromHours(6)) ?? string.Empty;
    }

    public object? GetDataTypeByAlias(string contentTypeAlias, string propertyAlias)
    {
        return _runtimeCache.GetCacheItem(
            "ekmDataTypeAlias" + contentTypeAlias + propertyAlias,
            () => GetDataTypeAliasValue(contentTypeAlias, propertyAlias),
            TimeSpan.FromMinutes(60));
    }

    public object GetDataTypeById(Guid id)
    {
        var dataType = _dataTypeService.GetAsync(id).GetAwaiter().GetResult();
        return FormatDataType(dataType);
    }

    public IEnumerable<object> GetNonEkomDataTypes()
    {
        return _dataTypeService.GetAllAsync().GetAwaiter().GetResult()
            .Where(x => !x.EditorAlias.StartsWith("Ekom", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.SortOrder)
            .Select(x => new
            {
                guid = x.Key,
                name = x.Name,
                editorAlias = x.EditorAlias,
            });
    }

    public string UrlSegment(string value) => value.ToUrlSegment(_shortStringHelper);

    private IDataType? GetDataTypeCached(int typeId)
    {
        var cacheKey = $"ekm_dt_{typeId}";

        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
#if UMBRACO_18
            var keyAttempt = _idKeyMap.GetKeyForId(typeId, UmbracoObjectTypes.DataType);
            return keyAttempt.Success
                ? _dataTypeService.GetAsync(keyAttempt.Result).GetAwaiter().GetResult()
                : null;
#else
            return _dataTypeService.GetDataType(typeId);
#endif
        });
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

    private string? GetDefaultLanguageIsoCode()
    {
#if UMBRACO_18
        return _languageService.GetDefaultIsoCodeAsync().GetAwaiter().GetResult();
#else
        return _localizationService.GetDefaultLanguageIsoCode();
#endif
    }

    private IEnumerable<ILanguage> GetAllLanguages()
    {
#if UMBRACO_18
        return _languageService.GetAllAsync().GetAwaiter().GetResult();
#else
        return _localizationService.GetAllLanguages();
#endif
    }

    private object? GetDataTypeAliasValue(string contentTypeAlias, string propertyAlias)
    {
        var contentType = _contentTypeService.Get(contentTypeAlias);
        var property = contentType?.CompositionPropertyTypes.FirstOrDefault(x => x.Alias == propertyAlias);

        if (property == null)
        {
            return null;
        }

        var dataType = _dataTypeService.GetAsync(property.DataTypeKey).GetAwaiter().GetResult();
        return FormatDataType(dataType);
    }

    private object FormatDataType(IDataType? dataType)
    {
        if (dataType == null)
        {
            throw new Exceptions.HttpResponseException(HttpStatusCode.NotFound);
        }

        var propertyEditor = _propertyEditorCollection.FirstOrDefault(x => x.Alias == dataType.EditorAlias);

        return new
        {
            guid = dataType.Key,
            propertyEditorAlias = dataType.EditorAlias,
            preValues = dataType.ConfigurationData,
            view = dataType.EditorUiAlias ?? propertyEditor?.Alias,
        };
    }
}
