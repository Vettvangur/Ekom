using Ekom.Models.Import;
using Ekom.Tests.Objects;
using Ekom.Umb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public class ImportServiceSaveEventTests
{
    public static IEnumerable<object[]> StagingCases()
    {
        foreach (var alias in new[] { "ekmProduct", "ekmProductVariantGroup", "ekmProductVariant" })
        foreach (var recycle in new[] { false, true })
        foreach (var saveEvent in Enum.GetValues<ImportSaveEntEnum>())
        foreach (var preserve in new[] { false, true })
        foreach (var published in new[] { false, true })
            yield return [alias, recycle, saveEvent, preserve, published];
    }

    [Theory]
    [MemberData(nameof(StagingCases))]
    public void StagingProductAndChildrenOnlySave(string alias, bool recycle, ImportSaveEntEnum saveEvent,
        bool preserve, bool published)
    {
        using var fixture = new Fixture(recycle ? Fixture.RecycleId : Fixture.ProcessingId);
        var content = fixture.Content(alias);
        content.SetupGet(x => x.Published).Returns(published);
        var import = Import(saveEvent, preserve);
        import.CreateDate = new DateTime(2024, 1, 1);
        import.UpdateDate = new DateTime(2025, 1, 1);

        fixture.SaveEvent(content.Object, import, create: !published,
            recycle: recycle ? fixture.Recycle.Object : null,
            processing: recycle ? null : fixture.Processing.Object);

        fixture.ContentService.Verify(x => x.Save(content.Object, Fixture.SyncUser, null), Times.Once);
        Assert.Empty(fixture.PublishInvocations);
        Assert.Equal(import.CreateDate, content.Object.CreateDate);
        Assert.Equal(import.UpdateDate, content.Object.UpdateDate);
        Assert.DoesNotContain(fixture.ContentService.Invocations, x => x.Method.Name is "Move" or "AttemptMove");
        fixture.ContentService.Verify(x => x.GetById(It.IsAny<int>()), Times.Never);
    }

    [Theory]
    [InlineData("ekmProductVariantGroup")]
    [InlineData("ekmProductVariant")]
    public void ChildrenInRecycleCategoryDoNotMoveTheProduct(string alias)
    {
        using var fixture = new Fixture(Fixture.RecycleId);
        var content = fixture.Content(alias);

        fixture.SaveEvent(content.Object, Import(), true, fixture.Recycle.Object, fixture.Processing.Object);

        Assert.Empty(fixture.PublishInvocations);
        Assert.DoesNotContain(fixture.ContentService.Invocations, x => x.Method.Name is "Move" or "AttemptMove");
        Assert.Equal(Fixture.RecycleId, fixture.Product.Object.ParentId);
    }

    [Fact]
    public void ProductStillMovesFromRecycleToProcessingBeforeSave()
    {
        using var fixture = new Fixture(Fixture.RecycleId);
        var order = new List<string>();
#if NET8_0
        fixture.ContentService.Setup(x => x.AttemptMove(fixture.Product.Object, Fixture.ProcessingId, Fixture.SyncUser))
#else
        fixture.ContentService.Setup(x => x.Move(fixture.Product.Object, Fixture.ProcessingId, Fixture.SyncUser))
#endif
            .Callback(() =>
            {
                order.Add("move");
                fixture.Product.Object.ParentId = Fixture.ProcessingId;
            })
            .Returns(OperationResult.Succeed(fixture.EventMessages));
        fixture.ContentService.Setup(x => x.Save(fixture.Product.Object, Fixture.SyncUser, null))
            .Callback(() => order.Add("save"));

        fixture.SaveEvent(fixture.Product.Object, Import(), false, fixture.Recycle.Object, fixture.Processing.Object);

        Assert.Equal(new[] { "move", "save" }, order);
        Assert.Equal(Fixture.ProcessingId, fixture.Product.Object.ParentId);
        Assert.Empty(fixture.PublishInvocations);
    }

    [Theory]
    [InlineData("ekmProductVariantGroup", false, false, true)]
    [InlineData("ekmProductVariant", false, false, true)]
    [InlineData("ekmProductVariantGroup", true, false, false)]
    [InlineData("ekmProductVariant", true, false, false)]
    [InlineData("ekmProductVariantGroup", true, true, true)]
    [InlineData("ekmProductVariant", true, true, true)]
    public void OtherCategoriesKeepExistingPublishPolicy(string alias, bool preserve, bool published, bool expectPublish)
    {
        using var fixture = new Fixture(Fixture.LiveCategoryId);
        var content = fixture.Content(alias);
        content.SetupGet(x => x.Published).Returns(published);

        fixture.SaveEvent(content.Object, Import(preserve: preserve), !published,
            fixture.Recycle.Object, fixture.Processing.Object);

        Assert.Equal(expectPublish, fixture.PublishInvocations.Any());
    }

    [Fact]
    public void OtherCategoriesKeepExistingUnpublishPolicy()
    {
        using var fixture = new Fixture(Fixture.LiveCategoryId);

        fixture.SaveEvent(fixture.Variant.Object, Import(ImportSaveEntEnum.Unpublish), false,
            fixture.Recycle.Object, fixture.Processing.Object);

        fixture.ContentService.Verify(x => x.Save(fixture.Variant.Object, Fixture.SyncUser, null), Times.Once);
        Assert.Single(fixture.PublishInvocations, x => x.Method.Name == "Unpublish");
    }

    [Fact]
    public void NoStagingContextKeepsExistingBehaviorWithoutParentLookups()
    {
        using var fixture = new Fixture(Fixture.ProcessingId);

        fixture.SaveEvent(fixture.Variant.Object, Import(), true);

        Assert.NotEmpty(fixture.PublishInvocations);
        fixture.ContentService.Verify(x => x.GetById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GuardDoesNotTraverseBeyondTheProductsImmediateParent()
    {
        using var fixture = new Fixture(Fixture.LiveCategoryId);

        fixture.SaveEvent(fixture.Variant.Object, Import(), true, null, fixture.Processing.Object);

        Assert.NotEmpty(fixture.PublishInvocations);
        fixture.ContentService.Verify(x => x.GetById(Fixture.LiveCategoryId), Times.Never);
    }

    [Theory]
    [InlineData(Fixture.ProcessingId, false, false)]
    [InlineData(Fixture.RecycleId, false, false)]
    [InlineData(Fixture.LiveCategoryId, true, false)]
    [InlineData(Fixture.ProcessingId, false, true)]
    [InlineData(Fixture.RecycleId, false, true)]
    [InlineData(Fixture.LiveCategoryId, true, true)]
    public void GroupAndVariantImportPathsReceiveStagingContext(int productParentId, bool expectPublish, bool create)
    {
        using var fixture = new Fixture(productParentId);
        var variant = new ImportVariant
        {
            Identifier = "variant",
            NodeName = "Variant",
            Title = [],
            Comparer = "changed",
            PreservePublishStatus = false,
            SaveEvent = ImportSaveEntEnum.SavePublish,
        };
        var group = new ImportVariantGroup
        {
            Identifier = "group",
            NodeName = "Group",
            Title = [],
            Comparer = "changed",
            PreservePublishStatus = false,
            SaveEvent = ImportSaveEntEnum.SavePublish,
            Variants = [variant],
        };

        fixture.IterateVariantGroups(group, create);

        Assert.Equal(expectPublish, fixture.PublishInvocations.Any());
        fixture.ContentService.Verify(x => x.GetById(It.IsAny<int>()), Times.Never);
        if (!expectPublish)
        {
            fixture.ContentService.Verify(x => x.Save(fixture.Group.Object, Fixture.SyncUser, null), Times.Once);
            fixture.ContentService.Verify(x => x.Save(fixture.Variant.Object, Fixture.SyncUser, null), Times.Once);
        }
        Assert.DoesNotContain(fixture.ContentService.Invocations, x => x.Method.Name is "Move" or "AttemptMove");
    }

    private static ImportBase Import(ImportSaveEntEnum saveEvent = ImportSaveEntEnum.SavePublish, bool preserve = false)
        => new() { Identifier = "item", NodeName = "Item", Title = [], SaveEvent = saveEvent, PreservePublishStatus = preserve };

    private sealed class Fixture : IDisposable
    {
        public const int ProcessingId = 10;
        public const int RecycleId = 20;
        public const int LiveCategoryId = 30;
        public const int SyncUser = 42;
        private readonly ConfigurationScope _scope = new(addServices: services =>
        {
            services.AddMemoryCache();
            var dataTypes = new Mock<IDataTypeService>();
            dataTypes.Setup(x => x.GetByEditorAlias(It.IsAny<string>())).Returns([Mock.Of<IDataType>()]);
            services.AddSingleton(dataTypes.Object);
        });
        private readonly ImportService _service;
        private readonly EventMessages _eventMessages = new();
        public EventMessages EventMessages => _eventMessages;
        public Mock<IContentService> ContentService { get; } = new();
        public Mock<IContent> Product { get; }
        public Mock<IContent> Group { get; }
        public Mock<IContent> Variant { get; }
        public Mock<IContent> Processing { get; } = Node(ProcessingId, -1, "ekmCategory");
        public Mock<IContent> Recycle { get; } = Node(RecycleId, -1, "ekmCategory");
        public IEnumerable<Moq.IInvocation> PublishInvocations => ContentService.Invocations
            .Where(x => x.Method.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase));

        public Fixture(int productParentId)
        {
            Product = Node(100, productParentId, "ekmProduct");
            Product.SetupGet(x => x.Published).Returns(true);
            Group = Node(200, 100, "ekmProductVariantGroup");
            Variant = Node(300, 200, "ekmProductVariant");
            Group.Setup(x => x.GetValue<string>(Configuration.ImportAliasIdentifier, null, null, false)).Returns("group");
            Variant.Setup(x => x.GetValue<string>(Configuration.ImportAliasIdentifier, null, null, false)).Returns("variant");
#if NET8_0
            ContentService.Setup(x => x.AttemptMove(It.IsAny<IContent>(), It.IsAny<int>(), It.IsAny<int>()))
#else
            ContentService.Setup(x => x.Move(It.IsAny<IContent>(), It.IsAny<int>(), It.IsAny<int>()))
#endif
                .Returns(OperationResult.Succeed(_eventMessages));
            ContentService.Setup(x => x.Delete(It.IsAny<IContent>(), It.IsAny<int>()))
                .Returns(OperationResult.Succeed(_eventMessages));
            ContentService.Setup(x => x.GetById(It.IsAny<int>()))
                .Throws(new InvalidOperationException("Import save paths must reuse the already loaded product."));
            ContentService.Setup(x => x.Create("Group", 100, It.IsAny<IContentType>(), SyncUser)).Returns(Group.Object);
            ContentService.Setup(x => x.Create("Variant", 200, It.IsAny<IContentType>(), SyncUser)).Returns(Variant.Object);
#if NET8_0
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!);
#else
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!, null!);
#endif
            SetField("productVariantGroupContentType", Group.Object.ContentType);
            SetField("productVariantContentType", Variant.Object.ContentType);
            SetField("variantGroupsSaved", new List<ImportVariantGroup>());
            SetField("variantsSaved", new List<ImportVariant>());
        }

        public Mock<IContent> Content(string alias) => alias switch
        {
            "ekmProduct" => Product,
            "ekmProductVariantGroup" => Group,
            "ekmProductVariant" => Variant,
            _ => throw new ArgumentException("Unexpected alias", nameof(alias)),
        };

        public void SaveEvent(IContent content, ImportBase import, bool create, IContent? recycle = null, IContent? processing = null)
            => Invoke("SaveEvent", [content, import, import.SaveEvent, import.PreservePublishStatus, SyncUser, create, recycle, processing, Product.Object]);

        public void IterateVariantGroups(ImportVariantGroup group, bool create)
            => Invoke("IterateVariantGroups", [new List<ImportVariantGroup> { group }, Product.Object,
                create ? new List<IContent> { Product.Object } : new List<IContent> { Product.Object, Group.Object, Variant.Object }, new List<IMedia>(), null,
                SyncUser, null, false, Recycle.Object, Processing.Object]);

        private void Invoke(string method, object?[] arguments)
            => typeof(ImportService).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_service, arguments);

        private void SetField(string name, object value)
            => typeof(ImportService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_service, value);

        public static Mock<IContent> Node(int id, int parentId, string alias)
        {
            var content = new Mock<IContent>();
            content.SetupGet(x => x.Id).Returns(id);
            content.SetupProperty(x => x.ParentId, parentId);
            content.SetupProperty(x => x.CreateDate);
            content.SetupProperty(x => x.UpdateDate);
            var contentType = new Mock<IContentType>();
            contentType.SetupGet(x => x.Alias).Returns(alias);
            var simpleContentType = contentType.As<ISimpleContentType>();
            simpleContentType.SetupGet(x => x.Alias).Returns(alias);
            content.SetupGet(x => x.ContentType).Returns(simpleContentType.Object);
            var propertyType = Mock.Of<IPropertyType>(x => x.PropertyEditorAlias == "Ekom.Property");
            var title = Mock.Of<IProperty>(x => x.Alias == "title" && x.PropertyType == propertyType);
            content.SetupGet(x => x.Properties).Returns(new PropertyCollection([title]));
            return content;
        }

        public void Dispose()
        {
            _eventMessages.Dispose();
            _scope.Dispose();
        }
    }
}
