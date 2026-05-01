using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Umb;

/// <summary>
/// Hooks Ekom into the Umbraco 17 application startup lifecycle.
/// </summary>
public sealed class EkomComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddEkom(builder.Config);
    }
}
