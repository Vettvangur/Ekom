using Ekom.Models.Import;
using Ekom.Umb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Data;
using System.Reflection;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.Mappers;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Xunit;
using IScope = Umbraco.Cms.Infrastructure.Scoping.IScope;
using IScopeProvider = Umbraco.Cms.Infrastructure.Scoping.IScopeProvider;

namespace Ekom.Tests.Tests;

public class ImportMediaLookupTests
{
    [Theory]
    [InlineData(false, ImportMediaContentTypes.images)]
    [InlineData(true, ImportMediaContentTypes.images)]
    [InlineData(false, ImportMediaContentTypes.files)]
    [InlineData(true, ImportMediaContentTypes.files)]
    public void MediaLookupCollectsIncomingAndUntouchedPickerReferences(bool json, ImportMediaContentTypes picker)
    {
        var fixture = new Fixture();
        var untouched = Media(10);
        var replaced = Media(20);
        var replacement = Media(30);
        var incoming = Media(40);
        var otherField = Media(50);
        var content = Content(1, "product");
        SetPicker(content, picker.ToString(), json, untouched, replaced);
        SetPicker(content, picker == ImportMediaContentTypes.images ? "files" : "images", json, otherField);
        fixture.SqlIds.AddRange([10, 20, 30, 40]);
        fixture.Descendants.AddRange([untouched.Object, replaced.Object, replacement.Object, incoming.Object]);

        var result = fixture.LookupMedia(content.Object,
        [
            External("existing-identity", "replacement-comparer"),
            new ImportMediaFromUdi { Udi = Udi(incoming) },
            new ImportMediaFromUdi { Udi = Udi(incoming) },
        ], picker);

        Assert.Equal(fixture.Descendants, result);
        var query = Assert.Single(fixture.SqlCalls);
        Assert.Equal("%,100,%", query.Parameters[0]);
        Assert.Equal("Image", query.Parameters[1]);
        Assert.Equal("File", query.Parameters[2]);
        Assert.Contains("n.trashed = 0", query.Sql, StringComparison.Ordinal);
        Assert.Contains("n.path LIKE @0", query.Sql, StringComparison.Ordinal);
        Assert.Contains("n.uniqueId IN (@3)", query.Sql, StringComparison.Ordinal);
        Assert.Contains("pt.alias = 'ekmIdentifier'", query.Sql, StringComparison.Ordinal);
        Assert.Contains("pt.alias = 'comparer'", query.Sql, StringComparison.Ordinal);
        var keys = Assert.IsAssignableFrom<IReadOnlyCollection<Guid>>(query.Parameters[3]);
        Assert.Equal(3, keys.Count);
        Assert.Contains(untouched.Object.Key, keys);
        Assert.Contains(replaced.Object.Key, keys);
        Assert.Contains(incoming.Object.Key, keys);
        Assert.DoesNotContain(otherField.Object.Key, keys);
        Assert.Equal(new[] { "existing-identity" }, Assert.IsAssignableFrom<IEnumerable<string>>(query.Parameters[4]));
        Assert.Equal(new[] { "replacement-comparer" }, Assert.IsAssignableFrom<IEnumerable<string>>(query.Parameters[5]));
        Assert.Contains(" OR ", query.Sql, StringComparison.Ordinal);
        Assert.NotNull(fixture.MediaFilter);
        AssertHydrationIds(fixture.MediaFilter, fixture.SqlIds);
        Assert.Equal(100, fixture.MediaParent);
        Assert.DoesNotContain(fixture.MediaService.Invocations, x => x.Method.Name == "GetById");
    }

    [Fact]
    public void MediaMatchesRetainDescendantOrderInsteadOfSqlIdOrder()
    {
        var fixture = new Fixture();
        var first = Media(30);
        var second = Media(10);
        var third = Media(20);
        fixture.SqlIds.AddRange([10, 20, 30]);
        fixture.Descendants.AddRange([first.Object, second.Object, third.Object]);

        var result = fixture.LookupMedia(Content(1, "product").Object, [External("identity", "comparer")]);

        Assert.Equal(new[] { first.Object, second.Object, third.Object }, result);
        Assert.Single(fixture.SqlCalls);
        Assert.Single(fixture.MediaService.Invocations);
    }

    [Fact]
    public void EmptyMediaSqlMatchDoesNotHydrateDescendants()
    {
        var fixture = new Fixture();

        var result = fixture.LookupMedia(Content(1, "product").Object, [External("missing", "missing")]);

        Assert.Empty(result);
        Assert.Single(fixture.SqlCalls);
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Fact]
    public void NoIncomingOrExistingMediaReferencesSkipsSqlAndHydration()
    {
        var fixture = new Fixture();

        Assert.Empty(fixture.LookupMedia(Content(1, "product").Object, []));

        Assert.Empty(fixture.SqlCalls);
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Fact]
    public void TargetedContentLookupIncludesUnpublishedSyncDisabledContentWithoutCatalogRoot()
    {
        var fixture = new Fixture();
        var target = Content(24, "identity", published: false, disabled: true);
        fixture.SqlIds.Add(24);
        fixture.Contents.Add(target.Object);

        var result = fixture.LookupContent("identity");

        Assert.Same(target.Object, result);
        Assert.False(result!.Published);
        Assert.True(result.GetValue<bool>("ekmDisableSync", null, null, false));
        AssertContentSql(fixture, "identity");
        Assert.NotNull(fixture.ContentFilter);
        AssertHydrationIds(fixture.ContentFilter, fixture.SqlIds);
        var hydrationSql = string.Join(" ", fixture.ContentFilter.GetWhereClauses().Select(x => x.Item1));
        Assert.Contains("trashed", hydrationSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("published", hydrationSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ekmDisableSync", hydrationSql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(7, fixture.HydratedContentType);
        Assert.Null(fixture.ContentOrdering);
        Assert.Single(fixture.ContentService.Invocations);
    }

    [Fact]
    public void DuplicateExactContentIdentifiersUseDefaultHydrationOrderNotSqlOrder()
    {
        var fixture = new Fixture();
        var first = Content(30, "shared");
        var second = Content(10, "shared");
        fixture.SqlIds.AddRange([10, 30]);
        fixture.Contents.AddRange([first.Object, second.Object]);

        Assert.Same(first.Object, fixture.LookupContent("shared"));

        AssertContentSql(fixture, "shared");
        Assert.Null(fixture.ContentOrdering);
        Assert.Single(fixture.ContentService.Invocations);
    }

    [Fact]
    public void CaseInsensitiveDatabaseCandidateDoesNotPassOrdinalContentIdentifierCheck()
    {
        var fixture = new Fixture();
        var candidate = Content(10, "IDENTITY");
        fixture.SqlIds.Add(10);
        fixture.Contents.Add(candidate.Object);

        Assert.Null(fixture.LookupContent("identity"));

        AssertContentSql(fixture, "identity");
        fixture.SqlIds.Add(20);
        var exact = Content(20, "identity");
        fixture.Contents.Add(exact.Object);
        Assert.Same(exact.Object, fixture.LookupContent("identity"));
    }

    [Fact]
    public void EmptyContentSqlMatchDoesNotHydrateContent()
    {
        var fixture = new Fixture();

        Assert.Null(fixture.LookupContent("missing"));

        AssertContentSql(fixture, "missing");
        Assert.Empty(fixture.ContentService.Invocations);
    }

    private static void AssertContentSql(Fixture fixture, string identifier)
    {
        var query = Assert.Single(fixture.SqlCalls);
        Assert.Equal(new object[] { 7, "ekmIdentifier", identifier }, query.Parameters);
        Assert.Contains("SELECT DISTINCT n.id", query.Sql, StringComparison.Ordinal);
        Assert.Contains("n.trashed = 0", query.Sql, StringComparison.Ordinal);
        Assert.Contains("cv.[current] = 1", query.Sql, StringComparison.Ordinal);
        Assert.Contains("c.contentTypeId = @0", query.Sql, StringComparison.Ordinal);
        Assert.Contains("pt.alias = @1", query.Sql, StringComparison.Ordinal);
        Assert.Contains("pd.varcharValue = @2", query.Sql, StringComparison.Ordinal);
        Assert.DoesNotContain("n.path", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("parentId", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("published", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ekmDisableSync", query.Sql, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertHydrationIds<T>(IQuery<T> filter, List<int> expectedIds)
    {
        var clauses = filter.GetWhereClauses().ToList();
        Assert.Contains(" IN ", string.Join(" ", clauses.Select(x => x.Item1)), StringComparison.OrdinalIgnoreCase);
        var parameters = clauses.SelectMany(x => x.Item2).OfType<int>().ToList();
        Assert.Equal(expectedIds, parameters);
    }

    private static Mock<IContent> Content(int id, string identifier, bool published = false, bool disabled = false)
    {
        var content = new Mock<IContent>();
        content.SetupGet(x => x.Id).Returns(id);
        content.SetupGet(x => x.Published).Returns(published);
        content.Setup(x => x.GetValue<string>("ekmIdentifier", null, null, false)).Returns(identifier);
        content.Setup(x => x.GetValue<bool>("ekmDisableSync", null, null, false)).Returns(disabled);
        return content;
    }

    private static Mock<IMedia> Media(int id)
    {
        var media = new Mock<IMedia>();
        media.SetupGet(x => x.Id).Returns(id);
        media.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        media.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(x => x.Alias == "Image"));
        return media;
    }

    private static string Udi(Mock<IMedia> media) => "umb://media/" + media.Object.Key.ToString("N");

    private static void SetPicker(Mock<IContent> content, string alias, bool json, params Mock<IMedia>[] media)
    {
        var value = json
            ? JsonSerializer.Serialize(media.Select(x => new { key = Guid.NewGuid(), mediaKey = x.Object.Key }))
            : string.Join(",", media.Select(Udi));
        content.Setup(x => x.GetValue<string>(alias, null, null, false)).Returns(value);
    }

    private static ImportMediaFromExternalUrl External(string identifier, string comparer) => new()
    {
        Identifier = identifier,
        Comparer = comparer,
        Url = "https://example.invalid/media.png",
        FileName = "media.png",
        NodeName = "Media",
    };

    private sealed class Fixture
    {
        private readonly ImportService _service;
        private readonly IContentType _contentType = Mock.Of<IContentType>(x => x.Id == 7);
        private readonly IMedia _root = Mock.Of<IMedia>(x => x.Id == 100);

        public Mock<IContentService> ContentService { get; } = new();
        public Mock<IMediaService> MediaService { get; } = new();
        public List<int> SqlIds { get; } = [];
        public List<(string Sql, object[] Parameters)> SqlCalls { get; } = [];
        public List<IContent> Contents { get; } = [];
        public List<IMedia> Descendants { get; } = [];
        public IQuery<IContent>? ContentFilter { get; private set; }
        public IQuery<IMedia>? MediaFilter { get; private set; }
        public Ordering? ContentOrdering { get; private set; }
        public int HydratedContentType { get; private set; }
        public int MediaParent { get; private set; }

        public Fixture()
        {
            var database = new Mock<IUmbracoDatabase>();
            database.Setup(x => x.Fetch<int>(It.IsAny<string>(), It.IsAny<object[]>()))
                .Callback((string sql, object[] parameters) => SqlCalls.Add((sql, parameters)))
                .Returns(() => SqlIds.ToList());
            var scope = new Mock<IScope>();
            scope.SetupGet(x => x.Database).Returns(database.Object);
            var scopeProvider = new Mock<IScopeProvider>();
            scopeProvider.Setup(x => x.CreateScope(IsolationLevel.Unspecified, RepositoryCacheMode.Unspecified,
                    null, null, null, false, true))
                .Returns(scope.Object);
            var sqlContext = new Mock<ISqlContext>();
            var syntax = new Mock<ISqlSyntaxProvider>();
            syntax.Setup(x => x.GetQuotedColumnName(It.IsAny<string>())).Returns((string name) => name);
            syntax.Setup(x => x.GetQuotedTableName(It.IsAny<string>())).Returns((string name) => name);
            sqlContext.SetupGet(x => x.SqlSyntax).Returns(syntax.Object);
            var configuration = new MapperConfigurationStore();
            var lazyContext = new Lazy<ISqlContext>(() => sqlContext.Object);
            sqlContext.SetupGet(x => x.Mappers).Returns(new MapperCollection(() =>
            [
                new ContentMapper(lazyContext, configuration),
                new MediaMapper(lazyContext, configuration),
            ]));
            scopeProvider.SetupGet(x => x.SqlContext).Returns(sqlContext.Object);

            ContentService.Setup(x => x.GetPagedOfType(It.IsAny<int>(), 0, int.MaxValue, out It.Ref<long>.IsAny,
                    It.IsAny<IQuery<IContent>>(), It.IsAny<Ordering>()))
                .Callback(new InvocationAction(invocation =>
                {
                    HydratedContentType = (int)invocation.Arguments[0];
                    ContentFilter = (IQuery<IContent>)invocation.Arguments[4];
                    ContentOrdering = (Ordering?)invocation.Arguments[5];
                }))
                .Returns(() => Contents);
            MediaService.Setup(x => x.GetPagedDescendants(It.IsAny<int>(), 0, It.IsAny<int>(), out It.Ref<long>.IsAny,
                    It.IsAny<IQuery<IMedia>>(), It.IsAny<Ordering>()))
                .Callback(new InvocationAction(invocation =>
                {
                    MediaParent = (int)invocation.Arguments[0];
                    MediaFilter = (IQuery<IMedia>)invocation.Arguments[4];
                }))
                .Returns(() => Descendants);

            var mediaImporter = new ImportMediaService(MediaService.Object, null!, null!, null!, null!, null!, null!,
                NullLogger<ImportMediaService>.Instance, scopeProvider.Object);
#if NET8_0
            _service = new ImportService(null!, ContentService.Object, null!, scopeProvider.Object, null!,
                NullLogger<ImportService>.Instance, null!, mediaImporter, null!);
#else
            _service = new ImportService(null!, ContentService.Object, null!, scopeProvider.Object, null!,
                NullLogger<ImportService>.Instance, null!, null!, mediaImporter, null!);
#endif
        }

        public IContent? LookupContent(string identifier)
            => (IContent?)Invoke("GetMediaSyncContent", [_contentType, identifier]);

        public List<IMedia> LookupMedia(IContent content, List<IImportMedia> imports,
            ImportMediaContentTypes picker = ImportMediaContentTypes.images)
            => (List<IMedia>)Invoke("GetMediaSyncMedia", [_root, content, imports, picker])!;

        private object? Invoke(string method, object?[] arguments)
            => typeof(ImportService).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_service, arguments);
    }
}
