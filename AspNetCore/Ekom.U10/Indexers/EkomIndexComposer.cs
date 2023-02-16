using Ekom.Umb.Indexers;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

public class EkomIndexComposer: IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        //composition.Services.AddExamineLuceneIndex<EkomIndex, ConfigurationEnabledDirectoryFactory>("EkomIndex");
        //composition.Services.ConfigureOptions<ConfigureEkomIndexOptions>();
        //composition.Services.AddSingleton<EkomIndexValueSetBuilder>();
        //composition.Services.AddSingleton<EkomIndexPopulator>();
        //builder.Services.ConfigureOptions<ConfigureExternalIndexOptions>();
        builder.Components().Insert<EkomIndexComponent>();
    }
}
