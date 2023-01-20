using Ekom.Models;
using Ekom.Services;
using Ekom.U8.Models;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Services;

namespace Ekom.U8.Services
{
    class UmbracoService : IUmbracoService
    {
        private readonly IDomainService _domainService;
        public UmbracoService(
            IDomainService domainService
        )
        {
            _domainService = domainService;
        }

        public IEnumerable<UmbracoDomain> GetDomains(bool includeWildcards = false)
        {
            return _domainService.GetAll(includeWildcards).Select(x => new Umbraco8Domain(x));
        }
    }
}
