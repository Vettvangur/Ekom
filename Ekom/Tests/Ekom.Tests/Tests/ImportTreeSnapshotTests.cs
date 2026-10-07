using Ekom.Events;
using Ekom.Models.Import;
using Ekom.Tests.Objects;
using Ekom.Umb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public class ImportTreeSnapshotTests
{
    [Theory]
    [InlineData(Fixture.CategoryId)]
    [InlineData(Fixture.RecycleId)]
    public void MovingOrRestoringProductRefreshesDescendantsBeforeTheirSaves(int parentId)
    {
        using var fixture = new Fixture(parentId);
        fixture.Iterate(fixture.ProductImport());

        fixture.AssertFreshChildrenSaved();
        Assert.Equal(Fixture.ProcessingId, fixture.Product.Object.ParentId);
        Assert.Equal(new[] { 200, 300 }, Assert.Single(fixture.ReadBatches).OrderBy(x => x));
        var calls = fixture.ContentService.Invocations.ToList();
        Assert.True(calls.FindIndex(x => x.Method.Name == Fixture.MoveMethod) < calls.FindIndex(x => x.Method.Name == "GetByIds"));
        Assert.True(calls.FindIndex(x => x.Method.Name == "GetByIds")
            < calls.FindIndex(x => x.Method.Name == "Save" && x.Arguments[0] is IContent content && content.Id is 200 or 300));
    }

    [Fact]
    public void SaveEventProcessingMoveRefreshesDescendantsBeforeTheirSaves()
    {
        using var fixture = new Fixture(Fixture.RecycleId);
        var product = fixture.ProductImport("recycle");
        product.Comparer = "changed";
        product.PreservePrimaryCategory = true;

        fixture.Iterate(product, delete: false);

        fixture.VerifyMove(Fixture.ProcessingId);
        fixture.AssertFreshChildrenSaved();
        Assert.Single(fixture.ReadBatches);
    }

    [Fact]
    public void ReconciliationAndSaveEventMovesCoalesceIntoOneFinalDescendantRefresh()
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        var product = fixture.ProductImport("missing");
        product.Categories = ["missing", "recycle"];
        product.Comparer = "changed";

        var failure = Record.Exception(() => fixture.Iterate(product));
        Assert.Null(product.Exception);
        Assert.Null(failure);

        fixture.VerifyMove(Fixture.RecycleId);
        fixture.VerifyMove(Fixture.ProcessingId);
        fixture.AssertFreshChildrenSaved();
        Assert.Single(fixture.ReadBatches);
    }

    [Fact]
    public void UnchangedProductTreeDoesNotReadContentAgain()
    {
        using var fixture = new Fixture(Fixture.ProcessingId);
        var product = fixture.ProductImport();
        product.VariantGroups[0].Comparer = "unchanged";
        product.VariantGroups[0].Variants[0].Comparer = "unchanged";

        fixture.Iterate(product);

        Assert.Empty(fixture.ContentService.Invocations);
        Assert.Same(fixture.Group.Object, fixture.Nodes.Single(x => x.Id == 200));
        Assert.Same(fixture.Variant.Object, fixture.Nodes.Single(x => x.Id == 300));
    }

    [Fact]
    public void MovingProductWithoutLoadedDescendantsDoesNotReadContentAgain()
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        fixture.Nodes.RemoveAll(x => x.Id is 200 or 300);
        var product = fixture.ProductImport();
        product.VariantGroups = [];

        fixture.Iterate(product);

        fixture.VerifyMove(Fixture.ProcessingId);
        fixture.AssertNoReads();
    }

    [Fact]
    public void DescendantRefreshUsesBoundedBatchesOfOnlyKnownIds()
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        var product = fixture.ProductImport();
        for (var i = 0; i < 501; i++)
        {
            var id = 1000 + i;
            fixture.AddDescendant(id, 200, "ekmProductVariant", $"variant-{id}");
            product.VariantGroups[0].Variants.Add(Fixture.VariantImport($"variant-{id}"));
        }

        fixture.Iterate(product);

        Assert.Equal(2, fixture.ReadBatches.Count);
        Assert.All(fixture.ReadBatches, batch => Assert.InRange(batch.Length, 1, 500));
        Assert.Equal(fixture.Fresh.Keys.OrderBy(x => x), fixture.ReadBatches.SelectMany(x => x).OrderBy(x => x));
        Assert.All(fixture.Fresh.Values, content => fixture.ContentService.Verify(x => x.Save(content, Fixture.SyncUser, null), Times.Once));
        fixture.AssertNoUnboundedReads();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void UnsuccessfulRequiredMoveDoesNotRelocateOrSaveDescendants(bool recycled, bool saveEvent)
    {
        using var fixture = new Fixture(recycled ? Fixture.RecycleId : Fixture.CategoryId);
        var originalParent = fixture.Product.Object.ParentId;
        fixture.CancelMoves();
        var product = fixture.ProductImport(saveEvent ? "recycle" : "processing");
        if (saveEvent)
        {
            product.Comparer = "changed";
            product.PreservePrimaryCategory = true;
        }

        var failure = Record.Exception(() => fixture.Iterate(product, delete: !saveEvent));

        Assert.True(failure != null || product.Exception != null, "A required canceled move must abort the product operation.");
        Assert.Equal(originalParent, fixture.Product.Object.ParentId);
        Assert.Equal(0, fixture.Count("productDeleted"));
        fixture.AssertOriginalChildrenNotSaved();
        fixture.AssertNoReads();
        Assert.Same(fixture.Group.Object, fixture.Nodes.Single(x => x.Id == 200));
    }

    [Fact]
    public void MissingRefreshedDescendantStopsImportBeforeSavingChildren()
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        fixture.ContentService.Setup(x => x.GetByIds(It.IsAny<IEnumerable<int>>()))
            .Returns(Array.Empty<IContent>());

        Assert.Throws<InvalidOperationException>(() => fixture.Iterate(fixture.ProductImport()));

        fixture.AssertOriginalChildrenNotSaved();
        fixture.ContentService.Verify(x => x.Save(fixture.Fresh[200], Fixture.SyncUser, null), Times.Never);
        fixture.ContentService.Verify(x => x.Save(fixture.Fresh[300], Fixture.SyncUser, null), Times.Never);
    }

    [Fact]
    public void FailedMoveDoesNotRelocateOrSaveDescendants()
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        fixture.FailMoves();
        var product = fixture.ProductImport();

        var failure = Record.Exception(() => fixture.Iterate(product));

        Assert.True(failure != null || product.Exception != null);
        Assert.Equal(Fixture.CategoryId, fixture.Product.Object.ParentId);
        fixture.AssertOriginalChildrenNotSaved();
        fixture.AssertNoReads();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void RefreshRejectsTrashedOrNewlyDisabledDescendantsBeforeChildProcessing(bool trashed, bool disabled)
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        var refreshed = Mock.Get(fixture.Fresh[200]);
        refreshed.SetupGet(x => x.Trashed).Returns(trashed);
        refreshed.Setup(x => x.HasProperty("ekmDisableSync")).Returns(disabled);
        refreshed.Setup(x => x.GetValue<bool>("ekmDisableSync", null, null, false)).Returns(disabled);

        Assert.Throws<InvalidOperationException>(() => fixture.Iterate(fixture.ProductImport()));

        fixture.AssertOriginalChildrenNotSaved();
        fixture.ContentService.Verify(x => x.Save(fixture.Fresh[200], Fixture.SyncUser, null), Times.Never);
        fixture.ContentService.Verify(x => x.Delete(It.IsAny<IContent>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void MoveRefreshesLoadedDescendantsAcrossAnUnloadedIntermediateNode()
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        fixture.Nodes.Remove(fixture.Group.Object);
        var product = fixture.ProductImport();
        product.VariantGroups = [];

        fixture.Iterate(product);

        Assert.Equal(new[] { 300 }, Assert.Single(fixture.ReadBatches));
        Assert.Same(fixture.Fresh[300], fixture.Nodes.Single(x => x.Id == 300));
        fixture.AssertNoUnboundedReads();
    }

    [Fact]
    public void ManySiblingGroupDeletionsCompactIndexesWithoutRepeatedSiblingScans()
    {
        using var fixture = new Fixture(Fixture.ProcessingId);
        fixture.Nodes.RemoveAll(x => x.Id is 200 or 300);
        const int siblingCount = 300;
        var idReads = 0;
        for (var i = 0; i < siblingCount; i++)
        {
            var id = 1000 + i;
            var content = fixture.AddDescendant(id, 100, "ekmProductVariantGroup", $"group-{id}");
            Mock.Get(content).SetupGet(x => x.Id).Returns(() =>
            {
                idReads++;
                return id;
            });
        }
        var product = fixture.ProductImport();
        product.VariantGroups = [];
        idReads = 0;

        fixture.Iterate(product);

        Assert.Equal(siblingCount, fixture.Count("variantGroupDeleted"));
        Assert.DoesNotContain(fixture.Nodes, x => x.ContentType.Alias == "ekmProductVariantGroup");
        Assert.InRange(idReads, 1, siblingCount * 30);
        fixture.AssertNoReads();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ProductDeletionEvictsEntireKnownSubtreeOnlyWhenSuccessful(bool canceled, bool failed)
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        var nestedCategory = fixture.AddDescendant(400, 300, "ekmCategory", "nested-category");
        fixture.Categories.Add(nestedCategory);
        if (canceled)
            fixture.CancelDeletes();
        if (failed)
            fixture.FailDeletes();
        var unsuccessful = canceled || failed;
        var before = fixture.Nodes.ToArray();
        Func<ImportProductEvaluatingEventArgs, Task> exclude = args =>
        {
            args.ExcludeFromImport = true;
            return Task.CompletedTask;
        };
        ImportEvents.ProductImportEvaluating += exclude;
        try
        {
            Record.Exception(() => fixture.Iterate(fixture.ProductImport(), recycle: false));

            fixture.ContentService.Verify(x => x.Delete(fixture.Product.Object, Fixture.SyncUser), Times.Once);
            Assert.Equal(unsuccessful ? 0 : 1, fixture.Count("productDeleted"));
            if (unsuccessful)
            {
                Assert.Equal(before, fixture.Nodes);
                Assert.Contains(fixture.Product.Object, fixture.Products);
                Assert.Contains(nestedCategory, fixture.Categories);
            }
            else
            {
                Assert.DoesNotContain(fixture.Nodes, x => x.Id is 100 or 200 or 300 or 400);
                Assert.Empty(fixture.Products);
                Assert.DoesNotContain(nestedCategory, fixture.Categories);
            }
            fixture.AssertNoReads();
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= exclude;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsuccessfulRecycleMoveDoesNotCountProductAsDeleted(bool failed)
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        if (failed)
            fixture.FailMoves();
        else
            fixture.CancelMoves();
        var before = fixture.Nodes.ToArray();
        Func<ImportProductEvaluatingEventArgs, Task> exclude = args =>
        {
            args.ExcludeFromImport = true;
            return Task.CompletedTask;
        };
        ImportEvents.ProductImportEvaluating += exclude;
        try
        {
            Assert.NotNull(Record.Exception(() => fixture.Iterate(fixture.ProductImport())));

            fixture.VerifyMove(Fixture.RecycleId);
            Assert.Equal(Fixture.CategoryId, fixture.Product.Object.ParentId);
            Assert.Equal(0, fixture.Count("productDeleted"));
            Assert.Equal(before, fixture.Nodes);
            fixture.AssertOriginalChildrenNotSaved();
            fixture.AssertNoReads();
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= exclude;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GroupDeletionEvictsDescendantsFromSharedNodesAndParentIndexOnlyWhenSuccessful(bool canceled)
    {
        using var fixture = new Fixture(Fixture.ProcessingId);
        fixture.AddDescendant(400, 300, "ekmProductVariant", "nested");
        var index = fixture.Nodes.GroupBy(x => x.ParentId).ToDictionary(x => x.Key, x => x.ToList());
        var before = fixture.Nodes.ToArray();
        if (canceled)
            fixture.CancelDeletes();

        Record.Exception(() => fixture.Invoke("IterateVariantGroups", [new List<ImportVariantGroup>(), fixture.Product.Object,
            fixture.Nodes, new List<IMedia>(), null, Fixture.SyncUser, index, false, fixture.Recycle.Object, fixture.Processing.Object]));

        Assert.Equal(canceled ? 0 : 1, fixture.Count("variantGroupDeleted"));
        if (canceled)
        {
            Assert.Equal(before, fixture.Nodes);
            Assert.Contains(fixture.Group.Object, index[100]);
            Assert.Contains(fixture.Variant.Object, index[200]);
        }
        else
        {
            Assert.DoesNotContain(fixture.Nodes, x => x.Id is 200 or 300 or 400);
            Assert.DoesNotContain(index.Values.SelectMany(x => x), x => x.Id is 200 or 300 or 400);
            Assert.Contains(fixture.Product.Object, fixture.Nodes);
        }
        fixture.AssertNoReads();
    }

    [Fact]
    public void CategoryMoveRefreshesKnownSubcategoriesBeforeRecursiveSaves()
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        var category = fixture.Categories.Single(x => x.Id == Fixture.CategoryId);
        var child = fixture.AddDescendant(400, Fixture.CategoryId, "ekmCategory", "child");
        fixture.Categories.Add(child);
        var fresh = Fixture.Node(400, Fixture.CategoryId, "ekmCategory", "child", "-1,1,2,30,10,400", 5).Object;
        fixture.ContentService.Setup(x => x.GetByIds(It.IsAny<IEnumerable<int>>()))
            .Returns((IEnumerable<int> ids) =>
            {
                Assert.Equal(Fixture.ProcessingId, category.ParentId);
                var batch = ids.ToArray();
                fixture.ReadBatches.Add(batch);
                Assert.Equal(new[] { 400 }, batch);
                return new[] { fresh };
            });
        var import = new ImportCategory
        {
            Identifier = "category", NodeName = "Category", Title = [], Comparer = "unchanged",
            ParentIdentifier = "processing",
            SubCategories =
            [
                new ImportCategory
                {
                    Identifier = "child", NodeName = "Child", Title = [], Comparer = "changed",
                    ParentIdentifier = "category", PreservePublishStatus = true,
                },
            ],
        };

        fixture.IterateCategories([import], delete: false);

        Assert.Same(fresh, fixture.Categories.Single(x => x.Id == 400));
        fixture.ContentService.Verify(x => x.Save(fresh, Fixture.SyncUser, null), Times.Once);
        fixture.ContentService.Verify(x => x.Save(child, Fixture.SyncUser, null), Times.Never);
        Assert.NotEqual(child.Path, fresh.Path);
        Assert.NotEqual(child.Level, fresh.Level);
        Assert.Single(fixture.ReadBatches);
        fixture.AssertNoUnboundedReads();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CategoryDeletionEvictsKnownSubcategoriesOnlyWhenSuccessful(bool canceled)
    {
        using var fixture = new Fixture(Fixture.CategoryId);
        fixture.Categories.RemoveAll(x => x.Id != Fixture.CategoryId);
        fixture.Categories.Add(fixture.AddDescendant(400, Fixture.CategoryId, "ekmCategory", "child"));
        fixture.Categories.Add(fixture.AddDescendant(401, 400, "ekmCategory", "grandchild"));
        var before = fixture.Categories.ToArray();
        if (canceled)
            fixture.CancelDeletes();

        Record.Exception(() => fixture.IterateCategories([], delete: true));

        Assert.Equal(canceled ? 0 : 1, fixture.Count("categoriesDeleted"));
        if (canceled)
            Assert.Equal(before, fixture.Categories);
        else
            Assert.Empty(fixture.Categories);
        fixture.AssertNoReads();
    }

    private sealed class Fixture : IDisposable
    {
        public const int CategoryId = 10;
        public const int RecycleId = 20;
        public const int ProcessingId = 30;
        public const int SyncUser = 42;
#if NET8_0
        public const string MoveMethod = "AttemptMove";
        private static readonly Expression<Func<IContentService, OperationResult>> Moves
            = x => x.AttemptMove(It.IsAny<IContent>(), It.IsAny<int>(), It.IsAny<int>());
#else
        public const string MoveMethod = "Move";
        private static readonly Expression<Func<IContentService, OperationResult>> Moves
            = x => x.Move(It.IsAny<IContent>(), It.IsAny<int>(), It.IsAny<int>());
#endif
        private readonly ConfigurationScope _scope = new(addServices: services =>
        {
            services.AddMemoryCache();
            var dataTypes = new Mock<IDataTypeService>();
            dataTypes.Setup(x => x.GetByEditorAlias(It.IsAny<string>())).Returns([Mock.Of<IDataType>()]);
            services.AddSingleton(dataTypes.Object);
        });
        private readonly ImportService _service;
        private readonly EventMessages _eventMessages = new();
        public Mock<IContentService> ContentService { get; } = new();
        public Mock<IContent> Product { get; }
        public Mock<IContent> Group { get; }
        public Mock<IContent> Variant { get; }
        public Mock<IContent> Recycle { get; } = Node(RecycleId, 1, "ekmCategory", "recycle", "-1,1,20", 2);
        public Mock<IContent> Processing { get; } = Node(ProcessingId, 2, "ekmCategory", "processing", "-1,1,2,30", 3);
        public List<IContent> Nodes { get; }
        public List<IContent> Products { get; }
        public List<IContent> Categories { get; }
        public Dictionary<int, IContent> Fresh { get; } = new();
        public List<int[]> ReadBatches { get; } = [];

        public Fixture(int parentId)
        {
            var category = Node(CategoryId, 1, "ekmCategory", "category", "-1,1,10", 2);
            Product = Node(100, parentId, "ekmProduct", "product", $"-1,1,{parentId},100", 3);
            Group = Node(200, 100, "ekmProductVariantGroup", "group", $"-1,1,{parentId},100,200", 4);
            Variant = Node(300, 200, "ekmProductVariant", "variant", $"-1,1,{parentId},100,200,300", 5);
            Nodes = [category.Object, Recycle.Object, Processing.Object, Product.Object, Group.Object, Variant.Object];
            Products = [Product.Object];
            Categories = [category.Object, Recycle.Object, Processing.Object];
            Fresh[200] = Node(200, 100, "ekmProductVariantGroup", "group", "-1,1,2,30,100,200", 5).Object;
            Fresh[300] = Node(300, 200, "ekmProductVariant", "variant", "-1,1,2,30,100,200,300", 6).Object;
            ContentService.Setup(Moves)
                .Callback<IContent, int, int>((content, parent, _) => content.ParentId = parent)
                .Returns(OperationResult.Succeed(_eventMessages));
            ContentService.Setup(x => x.Delete(It.IsAny<IContent>(), It.IsAny<int>()))
                .Returns(OperationResult.Succeed(_eventMessages));
            ContentService.Setup(x => x.Unpublish(It.IsAny<IContent>(), It.IsAny<string>(), It.IsAny<int>()))
                .Returns((IContent content, string _, int _) => new PublishResult(PublishResultType.SuccessUnpublish, _eventMessages, content));
            ContentService.Setup(x => x.GetByIds(It.IsAny<IEnumerable<int>>()))
                .Returns((IEnumerable<int> ids) =>
                {
                    Assert.Equal(ProcessingId, Product.Object.ParentId);
                    var batch = ids.ToArray();
                    ReadBatches.Add(batch);
                    return batch.Select(id => Fresh[id]).ToArray();
                });
            ContentService.Setup(x => x.GetById(It.IsAny<int>()))
                .Throws(new InvalidOperationException("Import must refresh only known descendants in bounded GetByIds batches."));
#if NET8_0
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!);
#else
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!, null!);
#endif
            SetField("umbracoRootContent", Node(1, -1, "ekmCatalog", "root", "-1,1", 1).Object);
            foreach (var field in new[] { "categoryContentType", "productContentType", "catalogContentType",
                         "productVariantGroupContentType", "productVariantContentType" })
                SetField(field, Mock.Of<IContentType>());
            SetField("productsSaved", new List<ImportProduct>());
            SetField("categoriesSaved", new List<ImportCategory>());
            SetField("variantGroupsSaved", new List<ImportVariantGroup>());
            SetField("variantsSaved", new List<ImportVariant>());
        }

        public ImportProduct ProductImport(string category = "processing") => new()
        {
            Identifier = "product",
            NodeName = "Product",
            Title = [],
            Comparer = "unchanged",
            Categories = [category],
            PreservePublishStatus = true,
            VariantGroups =
            [
                new ImportVariantGroup
                {
                    Identifier = "group", NodeName = "Group", Title = [], Comparer = "changed",
                    PreservePublishStatus = true, Variants = [VariantImport("variant")],
                },
            ],
        };

        public static ImportVariant VariantImport(string identifier) => new()
        {
            Identifier = identifier, NodeName = identifier, Title = [], Comparer = "changed", PreservePublishStatus = true,
        };

        public IContent AddDescendant(int id, int parentId, string alias, string identifier)
        {
            var parent = Nodes.Single(x => x.Id == parentId);
            var content = Node(id, parentId, alias, identifier, $"{parent.Path},{id}", parent.Level + 1).Object;
            Nodes.Add(content);
            var freshParent = Fresh.GetValueOrDefault(parentId) ?? Product.Object;
            Fresh[id] = Node(id, parentId, alias, identifier, $"{freshParent.Path},{id}", freshParent.Level + 1).Object;
            return content;
        }

        public void Iterate(ImportProduct product, bool delete = true, bool recycle = true)
            => Invoke("IterateProductTree", [new List<ImportProduct> { product }, Nodes, Products, Categories,
                new List<IMedia>(), null, SyncUser, delete, recycle ? Recycle.Object : null, Processing.Object, false, null]);

        public void IterateCategories(List<ImportCategory> categories, bool delete)
            => Invoke("IterateCategoryTree", [categories, categories, Categories, new List<IMedia>(), null,
                Node(1, -1, "ekmCatalog", "root", "-1,1", 1).Object, SyncUser, delete, null]);

        public void CancelMoves() => ContentService.Setup(Moves)
            .Returns(OperationResult.Cancel(_eventMessages));

        public void FailMoves() => ContentService.Setup(Moves)
            .Returns(new OperationResult(OperationResultType.FailedCannot, _eventMessages));

        public void VerifyMove(int parentId)
        {
#if NET8_0
            ContentService.Verify(x => x.AttemptMove(Product.Object, parentId, SyncUser), Times.Once);
#else
            ContentService.Verify(x => x.Move(Product.Object, parentId, SyncUser), Times.Once);
#endif
        }

        public void CancelDeletes() => ContentService.Setup(x => x.Delete(It.IsAny<IContent>(), It.IsAny<int>()))
            .Returns(OperationResult.Cancel(_eventMessages));

        public void FailDeletes() => ContentService.Setup(x => x.Delete(It.IsAny<IContent>(), It.IsAny<int>()))
            .Returns(new OperationResult(OperationResultType.FailedCannot, _eventMessages));

        public int Count(string field) => (int)typeof(ImportService)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_service)!;

        public void AssertFreshChildrenSaved()
        {
            foreach (var id in new[] { 200, 300 })
            {
                var fresh = Fresh[id];
                Assert.Same(fresh, Nodes.Single(x => x.Id == id));
                ContentService.Verify(x => x.Save(fresh, SyncUser, null), Times.Once);
                Assert.Contains(",30,100,", fresh.Path, StringComparison.Ordinal);
            }
            Assert.NotEqual(Group.Object.Path, Fresh[200].Path);
            Assert.NotEqual(Variant.Object.Level, Fresh[300].Level);
            AssertOriginalChildrenNotSaved();
            AssertNoUnboundedReads();
        }

        public void AssertOriginalChildrenNotSaved()
        {
            ContentService.Verify(x => x.Save(Group.Object, SyncUser, null), Times.Never);
            ContentService.Verify(x => x.Save(Variant.Object, SyncUser, null), Times.Never);
            Assert.DoesNotContain(ContentService.Invocations, x => x.Method.Name.Contains("Publish", StringComparison.Ordinal)
                && x.Arguments.Any(argument => ReferenceEquals(argument, Group.Object) || ReferenceEquals(argument, Variant.Object)));
        }

        public void AssertNoReads() => Assert.DoesNotContain(ContentService.Invocations,
            x => x.Method.Name.StartsWith("Get", StringComparison.Ordinal));

        public void AssertNoUnboundedReads() => Assert.DoesNotContain(ContentService.Invocations,
            x => x.Method.Name.StartsWith("Get", StringComparison.Ordinal) && x.Method.Name != "GetByIds");

        public object? Invoke(string method, object?[] arguments)
        {
            try
            {
                return typeof(ImportService).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(_service, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private void SetField(string name, object value) => typeof(ImportService)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_service, value);

        public static Mock<IContent> Node(int id, int parentId, string alias, string identifier, string path, int level)
        {
            var content = new Mock<IContent>();
            content.SetupGet(x => x.Id).Returns(id);
            content.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            content.SetupProperty(x => x.ParentId, parentId);
            content.SetupProperty(x => x.Name, identifier);
            content.SetupProperty(x => x.CreateDate);
            content.SetupProperty(x => x.UpdateDate);
            content.SetupGet(x => x.Path).Returns(path);
            content.SetupGet(x => x.Level).Returns(level);
            content.Setup(x => x.HasProperty(Configuration.ImportAliasIdentifier)).Returns(true);
            content.Setup(x => x.GetValue<string>(Configuration.ImportAliasIdentifier, null, null, false)).Returns(identifier);
            content.Setup(x => x.GetValue<string>("comparer", null, null, false)).Returns("unchanged");
            content.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(x => x.Alias == alias));
            var propertyType = Mock.Of<IPropertyType>(x => x.PropertyEditorAlias == "Ekom.Property");
            var title = Mock.Of<IProperty>(x => x.Alias == "title" && x.PropertyType == propertyType);
            var description = Mock.Of<IProperty>(x => x.Alias == "description" && x.PropertyType == propertyType);
            content.SetupGet(x => x.Properties).Returns(new PropertyCollection([title, description]));
            return content;
        }

        public void Dispose()
        {
            _eventMessages.Dispose();
            _scope.Dispose();
        }
    }
}
