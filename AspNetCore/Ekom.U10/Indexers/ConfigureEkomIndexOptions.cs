using Examine;
using Examine.Lucene;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;

namespace Ekom.Umb.Indexers
{
    public class ConfigureExternalIndexOptions : IConfigureNamedOptions<LuceneDirectoryIndexOptions>
    {
        public void Configure(string name, LuceneDirectoryIndexOptions options)
        {
            if (name.Equals(Constants.UmbracoIndexes.ExternalIndexName))
            {
                options.FieldDefinitions.AddOrUpdate(new FieldDefinition("ekmSearchPath", FieldDefinitionTypes.FullText));
            }
        }

        // Part of the interface, but does not need to be implemented for this.
        public void Configure(LuceneDirectoryIndexOptions options)
        {
            throw new System.NotImplementedException();
        }
    }
}


//using Examine;
//using Examine.Lucene;
//using Lucene.Net.Analysis.Standard;
//using Lucene.Net.Index;
//using Lucene.Net.Util;
//using Microsoft.Extensions.Options;
//using Umbraco.Cms.Core.Configuration.Models;
//using Umbraco.Cms.Core.Scoping;
//using Umbraco.Cms.Core.Services;
//using Umbraco.Cms.Infrastructure.Examine;

//namespace Ekom.Umb.Indexers
//{
//    internal class ConfigureEkomIndexOptions : IConfigureNamedOptions<LuceneDirectoryIndexOptions>
//    {
//        private readonly IOptions<IndexCreatorSettings> _settings;
//        private readonly IPublicAccessService _publicAccessService;
//        private readonly IScopeProvider _scopeProvider;

//        public ConfigureEkomIndexOptions(
//            IOptions<IndexCreatorSettings> settings,
//            IPublicAccessService publicAccessService,
//            IScopeProvider scopeProvider)
//        {
//            _settings = settings;
//            _publicAccessService = publicAccessService;
//            _scopeProvider = scopeProvider;
//        }

//        public void Configure(string name, LuceneDirectoryIndexOptions options)
//        {
//            if (name.Equals("EkomIndex"))
//            {
//                options.Analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
//                options.FieldDefinitions = new(
//                    new("id", FieldDefinitionTypes.Integer),
//                    new("name", FieldDefinitionTypes.FullText)
//                    );
//                options.UnlockIndex = true;
//                options.Validator = new ContentValueSetValidator(true, false, _publicAccessService, _scopeProvider, includeItemTypes: new[] { "ekmProduct" });
//                if (_settings.Value.LuceneDirectoryFactory == LuceneDirectoryFactory.SyncedTempFileSystemDirectoryFactory)
//                {
//                    // if this directory factory is enabled then a snapshot deletion policy is required
//                    options.IndexDeletionPolicy = new SnapshotDeletionPolicy(new KeepOnlyLastCommitDeletionPolicy());
//                }
//            }
//        }

//        public void Configure(LuceneDirectoryIndexOptions options)
//        {
//            throw new System.NotImplementedException();
//        }
//    }
//}
