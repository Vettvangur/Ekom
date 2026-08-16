using Ekom.Umb.SkuProductPicker.Models;

namespace Ekom.Umb.SkuProductPicker.Services;

public interface ISkuProductPickerService
{
    IReadOnlyList<SkuProductPickerItem> ResolveKeys(IReadOnlyList<Guid> keys);
    IReadOnlyList<SkuProductPickerItem> ResolveSkus(IReadOnlyList<string> skus);
}
