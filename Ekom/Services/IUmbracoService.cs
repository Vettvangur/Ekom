using Ekom.Models;
using Ekom.Models.Umbraco;

namespace Ekom.Services
{
    public interface IUmbracoService
    {
        IEnumerable<UmbracoDomain> GetDomains(bool includeWildcards = false);
        string GetDictionaryValue(string key);
        string GetDataType(string typeValue);
        IEnumerable<string> GetContent(string guid);
        IEnumerable<UmbracoLanguage> GetLanguages();
        string DefaultLanguage();
        object GetDataTypeByAlias(string contentTypeAlias, string propertyAlias);
        object GetDataTypeById(Guid id);
        IEnumerable<object> GetNonEkomDataTypes();
        string UrlSegment(string value);
    }
}
