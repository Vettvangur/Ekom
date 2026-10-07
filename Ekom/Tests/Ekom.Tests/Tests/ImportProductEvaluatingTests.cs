using Ekom.Events;
using Ekom.Models.Import;
using Ekom.Tests.Objects;
using Ekom.Umb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public class ImportProductEvaluatingTests
{
    [Fact]
    public void EventArgsExposeInputsAndExclusionCannotBeReversed()
    {
        var product = Product();
        var content = Mock.Of<IContent>();
        var rootKey = Guid.NewGuid();
        var args = new ImportProductEvaluatingEventArgs(product, content, rootKey);

        Assert.Same(product, args.ImportProduct);
        Assert.Same(content, args.ProductContent);
        Assert.Equal(rootKey, args.ImportRootKey);
        Assert.False(args.ExcludeFromImport);
        args.ExcludeFromImport = false;
        Assert.False(args.ExcludeFromImport);
        args.ExcludeFromImport = true;
        args.ExcludeFromImport = false;
        Assert.True(args.ExcludeFromImport);
        Assert.Null(typeof(ImportProductEvaluatingEventArgs).GetProperty(nameof(args.ImportProduct))!.SetMethod);
        Assert.Null(typeof(ImportProductEvaluatingEventArgs).GetProperty(nameof(args.ProductContent))!.SetMethod);
        Assert.Null(typeof(ImportProductEvaluatingEventArgs).GetProperty(nameof(args.ImportRootKey))!.SetMethod);
    }

    [Fact]
    public void NoSubscribersLeavesExistingProductEligibleAndUsesNormalSavePath()
    {
        using var fixture = new Fixture();
        var product = Product();
        var savesStarted = 0;
        Func<ImportProductEventArgs, Task> saving = args =>
        {
            Assert.Same(product, args.ImportProduct);
            Assert.Same(fixture.Existing.Object, args.ProductContent);
            savesStarted++;
            return Task.CompletedTask;
        };
        ImportEvents.ProductSaveStarting += saving;
        try
        {
            Assert.False(fixture.Evaluate(product, fixture.Existing.Object));
            Assert.False(fixture.Evaluate(Product("new"), null));
            fixture.Iterate([product]);

            Assert.Equal(1, savesStarted);
            fixture.AssertNoContentServiceCalls();
        }
        finally
        {
            ImportEvents.ProductSaveStarting -= saving;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExcludedExistingProductIsReconciledAsAbsent(bool recycle)
    {
        using var fixture = new Fixture();
        Func<ImportProductEvaluatingEventArgs, Task> handler = Exclude;
        ImportEvents.ProductImportEvaluating += handler;
        try
        {
            fixture.Iterate([Product()], recycle: recycle);

            if (recycle)
            {
                fixture.ContentService.Verify(x => x.Move(fixture.Existing.Object, Fixture.RecycleId, Fixture.SyncUser), Times.Once);
                Assert.Single(fixture.ContentService.Invocations, x => x.Method.Name == "Unpublish");
                Assert.Equal(new[] { "Unpublish", "Move" }, fixture.ContentService.Invocations.Select(x => x.Method.Name));
            }
            else
            {
                fixture.ContentService.Verify(x => x.Delete(fixture.Existing.Object, Fixture.SyncUser), Times.Once);
                Assert.Single(fixture.ContentService.Invocations);
            }
            Assert.Equal(1, fixture.DeletedCount);
            fixture.AssertNoReads();
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= handler;
        }
    }

    [Fact]
    public void ExclusionIsPerProductAndEligibleProductsStillReachTheSavePath()
    {
        using var fixture = new Fixture();
        var excluded = Product("new");
        var eligible = Product();
        var evaluated = new List<ImportProduct>();
        var saving = new List<ImportProduct>();
        Func<ImportProductEvaluatingEventArgs, Task> evaluating = args =>
        {
            evaluated.Add(args.ImportProduct);
            if (ReferenceEquals(args.ImportProduct, excluded))
                args.ExcludeFromImport = true;
            else
                Assert.False(args.ExcludeFromImport);
            return Task.CompletedTask;
        };
        Func<ImportProductEventArgs, Task> saveStarting = args =>
        {
            Assert.Equal(new[] { excluded, eligible }, evaluated);
            saving.Add(args.ImportProduct);
            return Task.CompletedTask;
        };
        ImportEvents.ProductImportEvaluating += evaluating;
        ImportEvents.ProductSaveStarting += saveStarting;
        try
        {
            fixture.Iterate([excluded, eligible]);

            Assert.Same(eligible, Assert.Single(saving));
            fixture.AssertNoContentServiceCalls();
            Assert.Equal(0, fixture.DeletedCount);
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= evaluating;
            ImportEvents.ProductSaveStarting -= saveStarting;
        }
    }

    [Fact]
    public void ExcludedNewProductReceivesNullContentAndNeverCreatesContent()
    {
        using var fixture = new Fixture();
        var product = Product("new");
        var calls = 0;
        Func<ImportProductEvaluatingEventArgs, Task> handler = args =>
        {
            calls++;
            Assert.Same(product, args.ImportProduct);
            Assert.Null(args.ProductContent);
            Assert.Equal(fixture.RootKey, args.ImportRootKey);
            return Exclude(args);
        };
        ImportEvents.ProductImportEvaluating += handler;
        try
        {
            fixture.Iterate([product], existing: []);

            Assert.Equal(1, calls);
            fixture.AssertNoContentServiceCalls();
            Assert.Equal(0, fixture.DeletedCount);
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= handler;
        }
    }

    [Fact]
    public void ExcludedProductDoesNotSaveOrReconcileItsVariantTree()
    {
        using var fixture = new Fixture();
        var product = Product();
        product.VariantGroups =
        [
            new ImportVariantGroup
            {
                Identifier = "group",
                NodeName = "Group",
                Title = [],
                Variants = [new ImportVariant { Identifier = "variant", NodeName = "Variant", Title = [] }],
            },
        ];
        var saveEvents = 0;
        Func<ImportProductEventArgs, Task> productSaving = _ => { saveEvents++; return Task.CompletedTask; };
        Func<ImportVariantEventArgs, Task> variantSaving = _ => { saveEvents++; return Task.CompletedTask; };
        Func<ImportProductEvaluatingEventArgs, Task> evaluating = Exclude;
        ImportEvents.ProductImportEvaluating += evaluating;
        ImportEvents.ProductSaveStarting += productSaving;
        ImportEvents.VariantSaveStarting += variantSaving;
        try
        {
            fixture.Iterate([product], delete: false, children:
            [
                Fixture.Node(200, Fixture.ProductId, "ekmProductVariantGroup", "stale-group").Object,
                Fixture.Node(300, 200, "ekmProductVariant", "stale-variant").Object,
            ]);

            Assert.Equal(0, saveEvents);
            fixture.AssertNoContentServiceCalls();
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= evaluating;
            ImportEvents.ProductSaveStarting -= productSaving;
            ImportEvents.VariantSaveStarting -= variantSaving;
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void ExclusionHonorsNonDeletingRecycledAndDisabledProtection(bool delete, bool recycled, bool disabled)
    {
        using var fixture = new Fixture(recycled, disabled);
        Func<ImportProductEvaluatingEventArgs, Task> handler = Exclude;
        ImportEvents.ProductImportEvaluating += handler;
        try
        {
            fixture.Iterate([Product()], delete: delete);

            fixture.AssertNoContentServiceCalls();
            Assert.Equal(0, fixture.DeletedCount);
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= handler;
        }
    }

    [Fact]
    public void OriginallyEmptyFeedDoesNotEvaluateOrDeleteExistingProducts()
    {
        using var fixture = new Fixture();
        var calls = 0;
        Func<ImportProductEvaluatingEventArgs, Task> handler = args => { calls++; return Exclude(args); };
        ImportEvents.ProductImportEvaluating += handler;
        try
        {
            fixture.Iterate([]);

            Assert.Equal(0, calls);
            fixture.AssertNoContentServiceCalls();
            Assert.Equal(0, fixture.DeletedCount);
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= handler;
        }
    }

    [Fact]
    public void OriginalFeedWithoutCategoriesFailsBeforeEvaluationAndReconciliation()
    {
        using var fixture = new Fixture();
        var product = Product();
        product.Categories = [];
        var calls = 0;
        Func<ImportProductEvaluatingEventArgs, Task> handler = args => { calls++; return Exclude(args); };
        ImportEvents.ProductImportEvaluating += handler;
        try
        {
            Assert.Throws<ArgumentException>(() => fixture.Iterate([product]));

            Assert.Equal(0, calls);
            fixture.AssertNoContentServiceCalls();
            Assert.Equal(0, fixture.DeletedCount);
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= handler;
        }
    }

    [Fact]
    public void LaterSubscriberFailurePreventsAllReconciliationAndSaves()
    {
        using var fixture = new Fixture();
        var first = Product();
        var later = Product("later");
        var evaluated = new List<ImportProduct>();
        var failure = new InvalidOperationException("Evaluation failed");
        Func<ImportProductEvaluatingEventArgs, Task> handler = async args =>
        {
            await Task.Yield();
            evaluated.Add(args.ImportProduct);
            if (ReferenceEquals(args.ImportProduct, later))
                throw failure;
            args.ExcludeFromImport = true;
        };
        ImportEvents.ProductImportEvaluating += handler;
        try
        {
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => fixture.Iterate([first, later])));

            Assert.Equal(new[] { first, later }, evaluated);
            fixture.AssertNoContentServiceCalls();
            Assert.Equal(0, fixture.DeletedCount);
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= handler;
        }
    }

    [Fact]
    public void AsyncSubscribersRunSequentiallyWithSharedStickyDecisionAndResolvedRoot()
    {
        using var fixture = new Fixture();
        var product = Product();
        var order = new List<string>();
        ImportProductEvaluatingEventArgs? firstArgs = null;
        Func<ImportProductEvaluatingEventArgs, Task> first = async args =>
        {
            firstArgs = args;
            Assert.Same(product, args.ImportProduct);
            Assert.Same(fixture.Existing.Object, args.ProductContent);
            Assert.Equal(fixture.RootKey, args.ImportRootKey);
            order.Add("first-start");
            await Task.Yield();
            args.ExcludeFromImport = true;
            order.Add("first-end");
        };
        Func<ImportProductEvaluatingEventArgs, Task> second = async args =>
        {
            Assert.Same(firstArgs, args);
            Assert.Equal(new[] { "first-start", "first-end" }, order);
            Assert.True(args.ExcludeFromImport);
            args.ExcludeFromImport = false;
            await Task.Yield();
            Assert.True(args.ExcludeFromImport);
            order.Add("second-end");
        };
        ImportEvents.ProductImportEvaluating += first;
        ImportEvents.ProductImportEvaluating += second;
        try
        {
            fixture.Iterate([product], delete: false);

            Assert.Equal(new[] { "first-start", "first-end", "second-end" }, order);
            fixture.AssertNoContentServiceCalls();
        }
        finally
        {
            ImportEvents.ProductImportEvaluating -= first;
            ImportEvents.ProductImportEvaluating -= second;
        }
    }

    private static Task Exclude(ImportProductEvaluatingEventArgs args)
    {
        args.ExcludeFromImport = true;
        return Task.CompletedTask;
    }

    private static ImportProduct Product(string identifier = "product") => new()
    {
        Identifier = identifier,
        NodeName = "Product",
        Title = [],
        Categories = ["category"],
        Comparer = "unchanged",
    };

    private sealed class Fixture : IDisposable
    {
        public const int CategoryId = 10;
        public const int RecycleId = 20;
        public const int ProductId = 100;
        public const int SyncUser = 42;
        private readonly ConfigurationScope _scope = new();
        private readonly ImportService _service;
        public Guid RootKey { get; } = Guid.NewGuid();
        public Mock<IContentService> ContentService { get; } = new();
        public Mock<IContent> Existing { get; }
        private Mock<IContent> Category { get; } = Node(CategoryId, 1, "ekmCategory", "category");
        private Mock<IContent> Recycle { get; } = Node(RecycleId, 1, "ekmCategory", "recycle");
        public int DeletedCount => (int)typeof(ImportService)
            .GetField("productDeleted", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_service)!;

        public Fixture(bool recycled = false, bool disabled = false)
        {
            Existing = Node(ProductId, recycled ? RecycleId : CategoryId, "ekmProduct", "product");
            Existing.Setup(x => x.GetValue<string>("comparer", null, null, false)).Returns("unchanged");
            Existing.Setup(x => x.HasProperty("ekmDisableSync")).Returns(disabled);
            Existing.Setup(x => x.GetValue<bool>("ekmDisableSync", null, null, false)).Returns(disabled);
            ContentService.Setup(x => x.GetById(It.IsAny<int>()))
                .Throws(new InvalidOperationException("Evaluation must reuse indexed content rather than read it again."));
#if NET8_0
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!);
#else
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, null!, null!);
#endif
            var root = Node(1, -1, "ekmCatalog", "root");
            root.SetupGet(x => x.Key).Returns(RootKey);
            SetField("umbracoRootContent", root.Object);
            foreach (var field in new[] { "categoryContentType", "productContentType", "catalogContentType",
                         "productVariantGroupContentType", "productVariantContentType" })
                SetField(field, Mock.Of<IContentType>());
            SetField("productsSaved", new List<ImportProduct>());
            SetField("variantGroupsSaved", new List<ImportVariantGroup>());
            SetField("variantsSaved", new List<ImportVariant>());
        }

        public bool Evaluate(ImportProduct product, IContent? content)
            => (bool)Invoke("IsProductExcludedFromImport", [product, content])!;

        public void Iterate(List<ImportProduct> products, bool delete = true, bool recycle = true,
            List<IContent>? existing = null, List<IContent>? children = null)
        {
            existing ??= [Existing.Object];
            var allNodes = new List<IContent>(existing) { Category.Object, Recycle.Object };
            if (children != null)
                allNodes.AddRange(children);
            Invoke("IterateProductTree", [products, allNodes, existing, new List<IContent> { Category.Object },
                new List<IMedia>(), null, SyncUser, delete, recycle ? Recycle.Object : null, null, false, null]);
        }

        public void AssertNoContentServiceCalls() => Assert.Empty(ContentService.Invocations);

        public void AssertNoReads()
            => Assert.DoesNotContain(ContentService.Invocations, x => x.Method.Name.StartsWith("Get", StringComparison.Ordinal));

        private object? Invoke(string method, object?[] arguments)
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

        private void SetField(string name, object value)
            => typeof(ImportService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_service, value);

        public static Mock<IContent> Node(int id, int parentId, string alias, string identifier)
        {
            var content = new Mock<IContent>();
            content.SetupGet(x => x.Id).Returns(id);
            content.SetupProperty(x => x.ParentId, parentId);
            content.Setup(x => x.HasProperty(Configuration.ImportAliasIdentifier)).Returns(true);
            content.Setup(x => x.GetValue<string>(Configuration.ImportAliasIdentifier, null, null, false)).Returns(identifier);
            var contentType = new Mock<ISimpleContentType>();
            contentType.SetupGet(x => x.Alias).Returns(alias);
            content.SetupGet(x => x.ContentType).Returns(contentType.Object);
            return content;
        }

        public void Dispose() => _scope.Dispose();
    }
}
