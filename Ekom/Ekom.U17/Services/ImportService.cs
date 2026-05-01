using Ekom.Models.Import;
using Ekom.Services;

namespace Ekom.Umb.Services;

internal sealed class ImportService : IImportService
{
    public void FullSync(ImportData data, Guid? parentKey = null, int syncUser = -1) => ThrowNotPorted();

    public void MoveSync(ImportData data, Guid? parentKey = null, int syncUser = -1) => ThrowNotPorted();

    public void CategorySync(ImportData data, Guid categoryKey, int syncUser = -1) => ThrowNotPorted();

    public void ProductSync(ImportProduct productData, Guid? parentKey, Guid mediaRootKey, int syncUser = -1, bool forceUpdate = false) => ThrowNotPorted();

    public void ProductUpdateSync(ImportProduct importProduct, Guid? parentKey, Guid mediaRootKey, int syncUser = -1, bool forceUpdate = false) => ThrowNotPorted();

    public void CategoryUpdateSync(ImportCategory importCategory, Guid? parentKey, int syncUser = -1) => ThrowNotPorted();

    public void VariantUpdateSync(ImportVariant importVariant, Guid? parentKey, int syncUser = -1) => ThrowNotPorted();

    public void VariantGroupSync(ImportVariantGroup importVariantGroup, Guid parentKey, Guid mediaRootKey, int syncUser = -1) => ThrowNotPorted();

    public void VariantSync(ImportVariant importVariant, Guid parentKey, Guid mediaRootKey, int syncUser = -1) => ThrowNotPorted();

    public void SyncProductMedia(string Identifier, List<IImportMedia> medias, Guid mediaRootKey, ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1) => ThrowNotPorted();

    public void SyncVariantMedia(string Identifier, List<IImportMedia> medias, Guid mediaRootKey, ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1) => ThrowNotPorted();

    private static void ThrowNotPorted()
    {
        throw new NotSupportedException("Import synchronization has not been ported to Umbraco 17 yet.");
    }
}
