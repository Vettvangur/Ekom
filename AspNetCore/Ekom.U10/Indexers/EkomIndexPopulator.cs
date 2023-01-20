using System;
using System.Collections.Generic;
using System.Linq;
using Examine;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Examine;

namespace Ekom.Umb.Indexers
{
    internal class EkomIndexPopulator : IndexPopulator
    {
        private readonly IContentService _contentService;
        private readonly EkomIndexValueSetBuilder _ekomIndexValueSetBuilder;

        public EkomIndexPopulator(IContentService contentService, EkomIndexValueSetBuilder ekomIndexValueSetBuilder)
        {
            _contentService = contentService;
            _ekomIndexValueSetBuilder = ekomIndexValueSetBuilder;
            RegisterIndex("EkomIndex");
        }
        protected override void PopulateIndexes(IReadOnlyList<IIndex> indexes)
        {
            foreach (var index in indexes)
            {
                var roots = _contentService.GetRootContent();
                index.IndexItems(_ekomIndexValueSetBuilder.GetValueSets(roots.ToArray()));

                foreach (var root in roots)
                {
                    var valueSets = _ekomIndexValueSetBuilder.GetValueSets(_contentService.GetPagedDescendants(root.Id, 0, Int32.MaxValue, out _).ToArray());
                    index.IndexItems(valueSets);
                }
            }

        }
    }
}
