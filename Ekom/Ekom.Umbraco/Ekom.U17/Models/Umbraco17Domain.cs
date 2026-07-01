using Umbraco.Cms.Core.Models;

namespace Ekom.Umb.Models;

internal sealed class Umbraco17Domain : Ekom.Models.UmbracoDomain
{
    public Umbraco17Domain(IDomain domain)
        : base(new Dictionary<string, string>
        {
            ["DomainName"] = domain.DomainName,
            ["Key"] = domain.Key.ToString(),
            ["LanguageIsoCode"] = domain.LanguageIsoCode ?? string.Empty,
            ["Id"] = domain.Id.ToString(),
            ["RootContentId"] = domain.RootContentId?.ToString() ?? string.Empty,
        })
    {
    }
}
