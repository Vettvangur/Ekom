using Umbraco.Core.Models;

namespace Ekom.Models
{
    public class Domain
    {
        private readonly IDomain value;

        public Domain(IDomain value)
        {
            this.value = value;

            DomainName = value.DomainName;
            IsWildcard = value.IsWildcard;
            LanguageId = value.LanguageId;
            LanguageIsoCode = value.LanguageIsoCode;
            RootContentId = value.RootContentId;
        }

        public Domain(string host, int rootContentId)
        {
            DomainName = host;
            //IsWildcard = value.IsWildcard;
            //LanguageId = value.LanguageId;
            //LanguageIsoCode = value.LanguageIsoCode;
            RootContentId = rootContentId;
        }

        public string DomainName { get; set; }
        public bool IsWildcard { get; set; }
        private int? LanguageId { get; set; }
        public string LanguageIsoCode { get; set; }
        public int? RootContentId { get; set; }

    }
}
