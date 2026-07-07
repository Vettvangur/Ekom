using Ekom.Models.Umbraco;
using Ekom.Services;
using Ekom.Umb.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Ekom.Umb.Services;

class UmbracoService : IUmbracoService
{
    private const string LanguagesCacheKey = "ekmLanguages";
    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<UmbracoLanguage>>> _languageLocks = new();
    private readonly IDataTypeService _dataTypeService;
    private readonly IDomainService _domainService;
    private readonly INodeService _nodeService;
    private readonly ILocalizationService _localizationService;
    private readonly PropertyEditorCollection _propertyEditorCollection;
    private readonly IContentTypeService _contentTypeService;
    private readonly IAppPolicyCache _runtimeCache;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IMemoryCache _cache;
    public UmbracoService(
        IDomainService domainService,
        IDataTypeService dataTypeService,
        ILocalizationService localizationService,
        PropertyEditorCollection propertyEditorCollection,
        IContentTypeService contentTypeService,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        INodeService nodeService,
        IMemoryCache cache)
    {
        _domainService = domainService;
        _dataTypeService = dataTypeService;
        _localizationService = localizationService;
        _propertyEditorCollection = propertyEditorCollection;
        _contentTypeService = contentTypeService;
        _runtimeCache = appCaches.RuntimeCache;
        _shortStringHelper = shortStringHelper;
        _nodeService = nodeService;
        _cache = cache;
    }

    public string GetDictionaryValue(string key)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<Ekom.Models.UmbracoDomain> GetDomains(bool includeWildcards = false)
    {
        return _domainService.GetAll(includeWildcards).Select(x => new Umbraco10Domain(x));
    }
    public string GetDataType(string typeValue)
    {

        if (int.TryParse(typeValue, out int typeValueInt))
        {
            var dt = GetDataTypeCached(typeValueInt);

            if (dt == null)
            {
                return string.Empty;
            }

            // FIX: verify
            typeValue = dt.ConfigurationAs<string>();
        }
        typeValue = typeValue.Contains('[') ? JsonConvert.DeserializeObject<string[]>(typeValue).FirstOrDefault() : typeValue;
        return typeValue;
    }

    private IDataType? GetDataTypeCached(int typeId)
    {
        // Normalize typeValue to ensure consistent cache keys
        string cacheKey = $"ekm_dt_{typeId}";

        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);


            return _dataTypeService.GetDataType(typeId);
        });
    }

    public IEnumerable<string> GetContent(string guid)
    {
        var nodes = guid
                ?.Split(',')
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => _nodeService.NodeById(GuidUdiHelper.GetGuid(x)))
                .Where(x => x != null)
                .ToList();
        return nodes.Select(x => x.Id.ToString());
    }

    public IEnumerable<object> GetNonEkomDataTypes()
    {
        return _dataTypeService.GetAll()
            .Where(x => !x.EditorAlias.StartsWith("Ekom"))
            .OrderBy(x => x.SortOrder)
            .Select(x => new
            {
                guid = x.Key,
                name = x.Name,
                editorAlias = x.EditorAlias
            });
    }

    public object GetDataTypeById(Guid id)
    {
        var dtd = _dataTypeService.GetDataType(id);
        return FormatDataType(dtd);
    }

    public object? GetDataTypeByAlias(
        string contentTypeAlias,
        string propertyAlias)
    {
        return _runtimeCache.GetCacheItem("ekmDataTypeAlias" + contentTypeAlias + propertyAlias, () => {
            return GetDataTypeAliasValue(contentTypeAlias, propertyAlias);
        }, TimeSpan.FromMinutes(60));
    }

    private object? GetDataTypeAliasValue(string contentTypeAlias,
        string propertyAlias)
    {
        var ct = _contentTypeService.Get(contentTypeAlias);

        var prop = ct?.CompositionPropertyTypes.FirstOrDefault(x => x.Alias == propertyAlias);

        if (prop == null)
        {
            return null;
        }

        var dtd = _dataTypeService.GetDataType(prop.DataTypeKey);
        return FormatDataType(dtd);
    }

    public IEnumerable<UmbracoLanguage>? GetLanguages()
    {
        return _runtimeCache.GetCacheItem(LanguagesCacheKey, () =>
        {
            var lazy = _languageLocks.GetOrAdd(LanguagesCacheKey, _ =>
                new Lazy<IReadOnlyList<UmbracoLanguage>>(
                    LoadLanguages,
                    LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                return lazy.Value;
            }
            catch
            {
                _languageLocks.TryRemove(LanguagesCacheKey, out _);
                throw;
            }
            finally
            {
                if (lazy.IsValueCreated)
                {
                    _languageLocks.TryRemove(LanguagesCacheKey, out _);
                }
            }
        }, TimeSpan.FromHours(6));
    }

    private IReadOnlyList<UmbracoLanguage> LoadLanguages()
    {
        return _localizationService.GetAllLanguages()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CultureName)
            .Select(x => new UmbracoLanguage()
            {
                Culture = x.CultureInfo,
                CultureName = x.CultureName,
                IsoCode = x.IsoCode
            })
            .ToList();
    }

    public string DefaultLanguage()
    {
        return _runtimeCache.GetCacheItem("ekmDefaultLanguage", () => {
            return _localizationService.GetDefaultLanguageIsoCode();
        }, TimeSpan.FromHours(6));
    }

    private object FormatDataType(IDataType dtd)
    {
        if (dtd == null)
            throw new Exceptions.HttpResponseException(HttpStatusCode.NotFound);

        var propertyEditor = _propertyEditorCollection.FirstOrDefault(x => x.Alias == dtd.EditorAlias);

        var preValues = dtd.Configuration;

        return new
        {
            guid = dtd.Key,
            propertyEditorAlias = dtd.EditorAlias,
            preValues = preValues,
            view = propertyEditor.GetValueEditor(null).View
        };
    }

    public string UrlSegment(string value)
    {
        return value.ToUrlSegment(_shortStringHelper);
    }
}
