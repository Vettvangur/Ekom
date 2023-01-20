using Ekom.Models;
using Ekom.Models.Umbraco;
using System;
using System.Collections.Generic;

namespace Ekom.Services
{
    public interface IUmbracoService
    {
        IEnumerable<UmbracoDomain> GetDomains(bool includeWildcards = false);
        string GetDictionaryValue(string key);
        string GetDataType(string typeValue);
        IEnumerable<string> GetContent(string guid);
        IEnumerable<UmbracoLanguage> GetLanguages();
        object GetDataTypeByAlias(string contentTypeAlias, string propertyAlias);
        object GetDataTypeById(Guid id);
        IEnumerable<object> GetNonEkomDataTypes();
        string UrlSegment(string value);
    }
}
