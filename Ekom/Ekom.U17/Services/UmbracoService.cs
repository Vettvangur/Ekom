using Ekom.Models.Umbraco;
using Ekom.Services;
using System.Globalization;
using Umbraco.Cms.Core.Services;

namespace Ekom.Umb.Services;

internal sealed class UmbracoService : IUmbracoService
{
    private readonly IDomainService _domainService;
    private readonly ILocalizationService _localizationService;

    public UmbracoService(IDomainService domainService, ILocalizationService localizationService)
    {
        _domainService = domainService;
        _localizationService = localizationService;
    }

    public IEnumerable<Ekom.Models.UmbracoDomain> GetDomains(bool includeWildcards = false)
    {
        return _domainService.GetAll(includeWildcards)
            .Select(x => new Ekom.Models.UmbracoDomain(new Dictionary<string, string>
            {
                ["DomainName"] = x.DomainName,
                ["Key"] = x.Key.ToString(),
                ["LanguageIsoCode"] = x.LanguageIsoCode ?? string.Empty,
                ["Id"] = x.Id.ToString(),
                ["RootContentId"] = x.RootContentId?.ToString() ?? string.Empty,
            }));
    }

    public string GetDictionaryValue(string key) => string.Empty;

    public string GetDataType(string typeValue) => typeValue;

    public IEnumerable<string> GetContent(string guid) => Array.Empty<string>();

    public IEnumerable<UmbracoLanguage> GetLanguages()
    {
        return _localizationService.GetAllLanguages()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CultureName)
            .Select(x => new UmbracoLanguage
            {
                Culture = x.CultureInfo ?? CultureInfo.InvariantCulture,
                CultureName = x.CultureName ?? string.Empty,
                IsoCode = x.IsoCode,
            })
            .ToArray();
    }

    public string DefaultLanguage() => _localizationService.GetDefaultLanguageIsoCode() ?? string.Empty;

    public object? GetDataTypeByAlias(string contentTypeAlias, string propertyAlias) => null;

    public object GetDataTypeById(Guid id) => throw new NotSupportedException("Data type lookup has not been ported to Umbraco 17 yet.");

    public IEnumerable<object> GetNonEkomDataTypes() => Array.Empty<object>();

    public string UrlSegment(string value) => value;
}
