using Ekom.Umb.VariantApp.Models;

namespace Ekom.Umb.VariantApp.Services;

public interface IVariantAppService
{
    VariantManagerProduct GetProductVariants(string productId);
    VariantManagerCount GetVariantCount(string productId);
    VariantManagerGroup CreateVariantGroup(VariantManagerGroupRequest request);
    Task<VariantManagerVariant> CreateVariantAsync(VariantManagerVariantRequest request);
    Task<VariantManagerProduct> SaveProductVariantsAsync(VariantManagerSaveRequest request);
    VariantManagerGroup SaveVariantGroup(VariantManagerGroupSaveRequest request);
    Task<VariantManagerVariant> SaveVariantAsync(VariantManagerVariantSaveRequest request);
    string GetMediaThumbnailPath(string mediaId, int width, int height);
    bool DeleteVariantNode(string nodeId);
}
