using Umbraco.Cms.Core.Models;

namespace Ekom.Umb.Models;

class Umbraco10Domain : Ekom.Models.UmbracoDomain
{
    public Umbraco10Domain(IDomain domain)
        : base(new Dictionary<string, string>
        {
            { "DomainName", domain.DomainName },
            { "Key", domain.Key.ToString() },
            { "LanguageIsoCode", domain.LanguageIsoCode },
            { "Id", domain.Id.ToString() },
            { "RootContentId", domain.RootContentId.HasValue ? domain.RootContentId.Value.ToString() : "" }
        })
    { }
}
