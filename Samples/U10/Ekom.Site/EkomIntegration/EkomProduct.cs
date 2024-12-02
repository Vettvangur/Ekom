using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Site.EkomIntegration;

public class ProductFac : IPerStoreFactory<IProduct>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProductFac(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public IProduct Create(UmbracoContent item, IStore store)
    {
        return new EkomProduct(item, store, _httpContextAccessor);
    }
}
public class EkomProduct : Product
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public EkomProduct(
        UmbracoContent content,
        IStore store,
        IHttpContextAccessor httpContextAccessor) : base(content, store)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Test => "test";


}
