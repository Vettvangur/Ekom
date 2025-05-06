using Ekom.Interfaces;
using Ekom.Models;

namespace Ekom.Site.EkomIntegration;

public class VariantGroupFac : IPerStoreFactory<IVariantGroup>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public VariantGroupFac(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public IVariantGroup Create(UmbracoContent item, IStore store)
    {
        return new EkomVariantGroup(item, store, _httpContextAccessor);
    }
}
public class EkomVariantGroup : VariantGroup
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public EkomVariantGroup(
        UmbracoContent content,
        IStore store,
        IHttpContextAccessor httpContextAccessor) : base(content, store)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Test => "test";


}
