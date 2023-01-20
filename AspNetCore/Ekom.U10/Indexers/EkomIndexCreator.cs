using System.Collections.Generic;
using Examine;
using Examine.Lucene.Analyzers;
using Examine.Lucene;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;

namespace Ekom.Umb.Indexers
{
    public class EkomIndexCreator //: LuceneIndexCreator, IUmbracoIndexesCreator
    {
        private readonly IProfilingLogger _profilingLogger;
        private readonly ILocalizationService _localizationService;
        private readonly IPublicAccessService _publicAccessService;


        public EkomIndexCreator(IProfilingLogger profilingLogger,
            ILocalizationService localizationService,
            IPublicAccessService publicAccessService
        )
        {
            _profilingLogger = profilingLogger;
            _localizationService = localizationService;
            _publicAccessService = publicAccessService;
        }


        //public override IEnumerable<IIndex> Create()
        //{
        //    var index = new UmbracoContentIndex("EkomIndex",
        //        CreateFileSystemLuceneDirectory("EkomIndex"),
        //        new UmbracoFieldDefinitionCollection(),
        //        new CultureInvariantWhitespaceAnalyzer(),
        //        _profilingLogger,
        //        _localizationService,

        //        new ContentValueSetValidator(true, false, _publicAccessService, includeItemTypes: new string[] { "ekmProduct", "ekmCategory", "ekmProductVariant", "ekmProductVariantGroup" }));

        //    return new[] { index };
        //}
    }
}
