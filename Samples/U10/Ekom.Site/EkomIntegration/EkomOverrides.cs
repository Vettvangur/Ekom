using Ekom.Interfaces;
using Ekom.Models;
using Umbraco.Cms.Core.Composing;

namespace Ekom.Site.EkomIntegration;

[ComposeAfter(typeof(Ekom.Umb.EkomComposer))]
class EkomOverrides : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {

        builder.Services.AddSingleton<IPerStoreFactory<IProduct>, ProductFac>();

    }
}
