using Umbraco.Cms.Core.Web;

namespace Ekom.Payments.Umb;

public class UmbracoPaymentExecutionContext : IPaymentExecutionContext
{
    readonly IUmbracoContextFactory _umbracoContextFactory;

    public UmbracoPaymentExecutionContext(IUmbracoContextFactory umbracoContextFactory)
    {
        _umbracoContextFactory = umbracoContextFactory;
    }

    public IDisposable EnsureContext()
    {
        return _umbracoContextFactory.EnsureUmbracoContext();
    }
}
