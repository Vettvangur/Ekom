using Ekom.Umb.Indexers;
using Umbraco.Cms.Core;
using Examine;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Examine;

public class EkomIndexComposer: IComposer
{
    public void Compose(IUmbracoBuilder composition)
    {
        composition.Services.AddExamineLuceneIndex<EkomIndex, ConfigurationEnabledDirectoryFactory>("EkomIndex");
        composition.Services.ConfigureOptions<ConfigureEkomIndexOptions>();
        composition.Services.AddSingleton<EkomIndexValueSetBuilder>();
        composition.Services.AddSingleton<EkomIndexPopulator>();
    }
}
