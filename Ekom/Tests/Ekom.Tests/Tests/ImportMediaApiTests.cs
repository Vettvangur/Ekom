using Ekom.Models.Import;
using Ekom.Services;
using Ekom.Umb.Services;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public class ImportMediaApiTests
{
    [Theory]
    [InlineData(nameof(IImportService.SyncProductMedia))]
    [InlineData(nameof(IImportService.SyncVariantMedia))]
    public void OriginalMediaSignaturesAndDefaultsAreRetained(string methodName)
    {
        foreach (var type in new[] { typeof(IImportService), typeof(ImportService) })
        {
            var method = MediaMethod(type, methodName, replacement: false);
            Assert.Equal(typeof(void), method.ReturnType);
            var parameters = method.GetParameters();
            Assert.Equal(6, parameters.Length);
            Assert.All(parameters.Take(5), parameter =>
            {
                Assert.False(parameter.IsOptional);
                Assert.False(parameter.HasDefaultValue);
            });
            Assert.Equal("syncUser", parameters[5].Name);
            Assert.True(parameters[5].IsOptional);
            Assert.Equal(-1, parameters[5].DefaultValue);
        }
    }

    [Theory]
    [InlineData(nameof(IImportService.SyncProductMedia))]
    [InlineData(nameof(IImportService.SyncVariantMedia))]
    public void ReplacementOverloadRequiresBooleanBeforeOptionalSyncUser(string methodName)
    {
        foreach (var type in new[] { typeof(IImportService), typeof(ImportService) })
        {
            var method = MediaMethod(type, methodName, replacement: true);
            Assert.Equal(typeof(void), method.ReturnType);
            var parameters = method.GetParameters();
            Assert.Equal(7, parameters.Length);
            Assert.Equal("replaceExisting", parameters[5].Name);
            Assert.Equal(typeof(bool), parameters[5].ParameterType);
            Assert.All(parameters.Take(6), parameter =>
            {
                Assert.False(parameter.IsOptional);
                Assert.False(parameter.HasDefaultValue);
            });
            Assert.Equal("syncUser", parameters[6].Name);
            Assert.Equal(typeof(int), parameters[6].ParameterType);
            Assert.True(parameters[6].IsOptional);
            Assert.Equal(-1, parameters[6].DefaultValue);
            Assert.False(MediaMethod(typeof(IImportService), methodName, replacement: true).IsAbstract);
        }
    }

    [Theory]
    [InlineData(nameof(IImportService.SyncProductMedia))]
    [InlineData(nameof(IImportService.SyncVariantMedia))]
    public void ConcreteServiceImplementsReplacementOverloadInsteadOfUsingInterfaceDefault(string methodName)
    {
        var interfaceMethod = MediaMethod(typeof(IImportService), methodName, replacement: true);
        var concreteMethod = MediaMethod(typeof(ImportService), methodName, replacement: true);
        var map = typeof(ImportService).GetInterfaceMap(typeof(IImportService));
        var index = Array.IndexOf(map.InterfaceMethods, interfaceMethod);

        Assert.True(index >= 0);
        Assert.Equal(concreteMethod, map.TargetMethods[index]);
        Assert.Equal(typeof(ImportService), map.TargetMethods[index].DeclaringType);
    }

    [Theory]
    [InlineData(false, ImportMediaContentTypes.images, 42)]
    [InlineData(false, ImportMediaContentTypes.files, -1)]
    [InlineData(true, ImportMediaContentTypes.images, -1)]
    [InlineData(true, ImportMediaContentTypes.files, 42)]
    public void LegacyImplementationDefaultMergeForwardsAllArguments(bool variant,
        ImportMediaContentTypes selectedField, int syncUser)
    {
        var legacy = new LegacyImportService();
        IImportService service = legacy;
        var identifier = variant ? "variant-identifier" : "product-identifier";
        List<IImportMedia> medias = [new ImportMediaFromUdi { Udi = "umb://media/" + Guid.NewGuid().ToString("N") }];
        var rootKey = Guid.NewGuid();
        var mediaType = selectedField == ImportMediaContentTypes.files ? ImportMediaTypes.File : ImportMediaTypes.Image;

        SyncMedia(service, variant, identifier, medias, rootKey, mediaType, selectedField,
            replaceExisting: false, syncUser: syncUser);

        var call = Assert.Single(legacy.Calls);
        Assert.Equal(variant, call.Variant);
        Assert.Equal(identifier, call.Identifier);
        Assert.Same(medias, call.Medias);
        Assert.Equal(rootKey, call.MediaRootKey);
        Assert.Equal(mediaType, call.MediaType);
        Assert.Equal(selectedField, call.SelectedField);
        Assert.Equal(syncUser, call.SyncUser);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyImplementationDefaultMergeUsesDefaultSyncUser(bool variant)
    {
        var legacy = new LegacyImportService();
        IImportService service = legacy;

        if (variant)
        {
            service.SyncVariantMedia("variant", [], Guid.NewGuid(), ImportMediaTypes.Image,
                ImportMediaContentTypes.images, replaceExisting: false);
        }
        else
        {
            service.SyncProductMedia("product", [], Guid.NewGuid(), ImportMediaTypes.Image,
                ImportMediaContentTypes.images, replaceExisting: false);
        }

        Assert.Equal(-1, Assert.Single(legacy.Calls).SyncUser);
    }

    [Theory]
    [InlineData(false, ImportMediaContentTypes.images)]
    [InlineData(false, ImportMediaContentTypes.files)]
    [InlineData(true, ImportMediaContentTypes.images)]
    [InlineData(true, ImportMediaContentTypes.files)]
    public void LegacyImplementationRejectsReplacementWithoutSideEffects(bool variant,
        ImportMediaContentTypes selectedField)
    {
        var legacy = new LegacyImportService();
        IImportService service = legacy;
        var media = new ImportMediaFromUdi { Udi = "umb://media/" + Guid.NewGuid().ToString("N") };
        List<IImportMedia> medias = [media];

        Assert.Throws<NotSupportedException>(() => SyncMedia(service, variant, "identifier", medias,
            Guid.NewGuid(), ImportMediaTypes.Image, selectedField, replaceExisting: true, syncUser: 42));

        Assert.Empty(legacy.Calls);
        Assert.Same(media, Assert.Single(medias));
    }

    private static MethodInfo MediaMethod(Type type, string name, bool replacement)
    {
        Type[] parameterTypes = replacement
            ? [typeof(string), typeof(List<IImportMedia>), typeof(Guid), typeof(ImportMediaTypes),
                typeof(ImportMediaContentTypes), typeof(bool), typeof(int)]
            : [typeof(string), typeof(List<IImportMedia>), typeof(Guid), typeof(ImportMediaTypes),
                typeof(ImportMediaContentTypes), typeof(int)];
        var method = type.GetMethod(name, parameterTypes);
        Assert.NotNull(method);
        return method;
    }

    private static void SyncMedia(IImportService service, bool variant, string identifier,
        List<IImportMedia> medias, Guid rootKey, ImportMediaTypes mediaType,
        ImportMediaContentTypes selectedField, bool replaceExisting, int syncUser)
    {
        if (variant)
        {
            service.SyncVariantMedia(identifier, medias, rootKey, mediaType, selectedField, replaceExisting, syncUser);
        }
        else
        {
            service.SyncProductMedia(identifier, medias, rootKey, mediaType, selectedField, replaceExisting, syncUser);
        }
    }

    private sealed record MediaCall(bool Variant, string Identifier, List<IImportMedia> Medias,
        Guid MediaRootKey, ImportMediaTypes MediaType, ImportMediaContentTypes SelectedField, int SyncUser);

    // Deliberately implements only the original required members, as an external custom service would.
    private sealed class LegacyImportService : IImportService
    {
        public List<MediaCall> Calls { get; } = [];

        public void SyncProductMedia(string Identifier, List<IImportMedia> medias, Guid mediaRootKey,
            ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1)
            => Calls.Add(new MediaCall(false, Identifier, medias, mediaRootKey, mediaType, mediaContentType, syncUser));

        public void SyncVariantMedia(string Identifier, List<IImportMedia> medias, Guid mediaRootKey,
            ImportMediaTypes mediaType, ImportMediaContentTypes mediaContentType, int syncUser = -1)
            => Calls.Add(new MediaCall(true, Identifier, medias, mediaRootKey, mediaType, mediaContentType, syncUser));

        public void FullSync(ImportData data, Guid? parentKey = null, int syncUser = -1)
            => throw new NotSupportedException();

        public void MoveSync(ImportData data, Guid? parentKey = null, int syncUser = -1)
            => throw new NotSupportedException();

        public void CategorySync(ImportData data, Guid categoryKey, int syncUser = -1)
            => throw new NotSupportedException();

        public void ProductSync(ImportProduct productData, Guid? parentKey, Guid mediaRootKey,
            int syncUser = -1, bool forceUpdate = false)
            => throw new NotSupportedException();

        public void ProductUpdateSync(ImportProduct importProduct, Guid? parentKey, Guid mediaRootKey,
            int syncUser = -1, bool forceUpdate = false)
            => throw new NotSupportedException();

        public void CategoryUpdateSync(ImportCategory importCategory, Guid? parentKey, int syncUser = -1)
            => throw new NotSupportedException();

        public void VariantUpdateSync(ImportVariant importVariant, Guid? parentKey, int syncUser = -1)
            => throw new NotSupportedException();

        public void VariantGroupSync(ImportVariantGroup importVariantGroup, Guid parentKey,
            Guid mediaRootKey, int syncUser = -1)
            => throw new NotSupportedException();

        public void VariantSync(ImportVariant importVariant, Guid parentKey, Guid mediaRootKey, int syncUser = -1)
            => throw new NotSupportedException();
    }
}
