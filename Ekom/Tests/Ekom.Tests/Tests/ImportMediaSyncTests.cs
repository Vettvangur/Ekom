using Ekom.Models.Import;
using Ekom.Umb.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

public class ImportMediaSyncTests
{
    [Fact]
    public void IdentifierTakesPrecedenceOverComparerWhenDeletingExternalMedia()
    {
        var fixture = new Fixture();
        var byComparer = fixture.Media("other", "new");
        var byIdentifier = fixture.Media("identity", "old");
        fixture.Picker("images", byIdentifier, byComparer);

        Assert.True(fixture.Import([External("identity", "new", action: ImportMediaAction.Delete)]));

        Assert.Equal(Udi(byComparer), fixture.Value("images"));
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Fact]
    public void ChangedExternalComparerReusesSeparateExistingMediaAndUpdatesSortOrder()
    {
        var fixture = new Fixture();
        var old = fixture.Media("identity", "old", 4);
        var replacement = fixture.Media("other", "new", 9);
        fixture.Picker("images", old);

        Assert.True(fixture.Import([External("identity", "new", 2)]));

        Assert.Equal(Udi(replacement), fixture.Value("images"));
        fixture.MediaService.Verify(x => x.Save(replacement.Object, -1), Times.Once);
        Assert.Equal(2, replacement.Object.GetValue<int>("ekmSortOrder"));
        Assert.DoesNotContain(fixture.MediaService.Invocations, x => x.Method.Name.StartsWith("Create", StringComparison.Ordinal));
    }

    [Fact]
    public void UntouchedExistingReferencesArePreservedAndSortedAlongsideImportedMedia()
    {
        var fixture = new Fixture();
        var last = fixture.Media("last", "last", 30);
        var first = fixture.Media("first", "first", 1);
        var added = fixture.Media("added", "added", 10);
        fixture.Picker("images", last, first);

        Assert.True(fixture.Import([new ImportMediaFromUdi { Udi = Udi(added) }]));

        Assert.Equal(string.Join(",", Udi(first), Udi(added), Udi(last)), fixture.Value("images"));
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Fact]
    public void EqualAndMissingSortOrdersKeepExistingThenIncomingOrder()
    {
        var fixture = new Fixture();
        var missingExisting = fixture.Media("missing-existing", "a");
        var equalExisting = fixture.Media("equal-existing", "b", 5);
        var equalIncoming = fixture.Media("equal-incoming", "c", 5);
        var missingIncoming = fixture.Media("missing-incoming", "d");
        fixture.Picker("images", missingExisting, equalExisting);
        List<IImportMedia> imports =
        [
            new ImportMediaFromUdi { Udi = Udi(missingIncoming) },
            new ImportMediaFromUdi { Udi = Udi(equalIncoming), SortOrder = 5 },
        ];

        Assert.True(fixture.Import(imports));
        Assert.Equal(string.Join(",", Udi(equalExisting), Udi(equalIncoming), Udi(missingExisting), Udi(missingIncoming)),
            fixture.Value("images"));
        fixture.ContentService.Invocations.Clear();
        Assert.False(fixture.Import(imports));
        Assert.Empty(fixture.ContentService.Invocations);
    }

    [Fact]
    public void RepeatedUdiAddsAndDeletesAreIdempotent()
    {
        var fixture = new Fixture();
        var removed = fixture.Media("removed", "old");
        var retained = fixture.Media("retained", "new");
        fixture.Picker("images", removed);
        List<IImportMedia> imports =
        [
            new ImportMediaFromUdi { Udi = Udi(removed), Action = ImportMediaAction.Delete },
            new ImportMediaFromUdi { Udi = Udi(removed), Action = ImportMediaAction.Delete },
            new ImportMediaFromUdi { Udi = Udi(retained) },
            new ImportMediaFromUdi { Udi = Udi(retained) },
        ];

        Assert.True(fixture.Import(imports));
        Assert.Equal(Udi(retained), fixture.Value("images"));
        fixture.ContentService.Invocations.Clear();
        Assert.False(fixture.Import(imports));
        Assert.Empty(fixture.ContentService.Invocations);
    }

    [Fact]
    public void ExistingJsonPickerReferencesParticipateInPreservationAndSorting()
    {
        var fixture = new Fixture();
        var existing = fixture.Media("existing", "existing", 20);
        var incoming = fixture.Media("incoming", "incoming", 1);
        fixture.SetPicker("images", JsonSerializer.Serialize(new[]
        {
            new { key = Guid.NewGuid(), mediaKey = existing.Object.Key },
        }));

        Assert.Equal(new[] { Udi(existing) }, fixture.CurrentUdis(ImportMediaContentTypes.images));
        Assert.True(fixture.Import([new ImportMediaFromUdi { Udi = Udi(incoming) }]));
        Assert.Equal(string.Join(",", Udi(incoming), Udi(existing)), fixture.Value("images"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedPickerPreservesPublishedStatusWhenSaving(bool published)
    {
        var fixture = new Fixture(published);
        var media = fixture.Media("media", "comparer");

        Assert.True(fixture.Import([new ImportMediaFromUdi { Udi = Udi(media) }]));

        var publish = fixture.ContentService.Invocations.Where(x => x.Method.Name.Contains("Publish", StringComparison.Ordinal)).ToList();
        if (published)
        {
            Assert.Single(publish);
            Assert.Equal(Fixture.SyncUser, publish[0].Arguments.Last());
#if NET8_0
            fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Never);
#else
            // SaveAndPublish is an extension that saves before calling Publish on newer Umbraco versions.
            fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Once);
#endif
        }
        else
        {
            Assert.Empty(publish);
            fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Once);
        }
    }

    [Fact]
    public void UnchangedPickerDoesNotSetValueOrSave()
    {
        var fixture = new Fixture();
        var media = fixture.Media("identity", "same", 2);
        fixture.Picker("images", media);

        Assert.False(fixture.Import([External("identity", "same", 2)]));

        Assert.DoesNotContain(fixture.Content.Invocations, x => x.Method.Name == "SetValue");
        Assert.Empty(fixture.ContentService.Invocations);
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Fact]
    public void SaveContentFalseChangesPickerWithoutPersistence()
    {
        var fixture = new Fixture();
        var media = fixture.Media("identity", "same");

        Assert.True(fixture.Import([new ImportMediaFromUdi { Udi = Udi(media) }], saveContent: false));

        Assert.Equal(Udi(media), fixture.Value("images"));
        Assert.Empty(fixture.ContentService.Invocations);
    }

    [Theory]
    [InlineData(ImportMediaTypes.Image, ImportMediaContentTypes.images)]
    [InlineData(ImportMediaTypes.File, ImportMediaContentTypes.files)]
    public void MatchingIdentifierAndComparerAreIsolatedByMediaType(ImportMediaTypes type, ImportMediaContentTypes picker)
    {
        var fixture = new Fixture();
        var image = fixture.Media("shared", "same", mediaType: ImportMediaTypes.Image);
        var file = fixture.Media("shared", "same", mediaType: ImportMediaTypes.File);
        fixture.Picker("images", image);
        fixture.Picker("files", file);
        var expected = type == ImportMediaTypes.Image ? image : file;
        var untouchedPicker = type == ImportMediaTypes.Image ? "files" : "images";
        var untouchedValue = fixture.Value(untouchedPicker);

        Assert.True(fixture.Import([External("shared", "same", action: ImportMediaAction.Delete)], type, picker));

        Assert.Equal("", fixture.Value(picker.ToString()));
        Assert.Equal(untouchedValue, fixture.Value(untouchedPicker));
        Assert.DoesNotContain(Udi(expected), fixture.Value(picker.ToString()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleMediaReferenceComparersForBinaryPayloadsIncludeSortOrderAndMatchReuse(bool base64)
    {
        var fixture = new Fixture();
        IImportMedia import = base64
            ? new ImportMediaFromBase64 { Base64 = "AQID", FileName = "media.png", NodeName = "Media", Identifier = "", SortOrder = 7 }
            : new ImportMediaFromBytes { Bytes = [1, 2, 3], FileName = "media.png", NodeName = "Media", Identifier = "", SortOrder = 7 };
        var excluded = base64 ? "Base64" : "Bytes";
        var comparer = fixture.Hash(import, excluded);
        Assert.NotEqual(comparer, fixture.Hash(import, excluded, "SortOrder"));
        var existing = fixture.Media("", comparer);

        var references = fixture.References([import], singleMedia: true);
        Assert.Equal(new[] { comparer }, references.Comparers);
        Assert.Empty(references.Identifiers);
        Assert.True(fixture.Import([import]));
        Assert.Equal(Udi(existing), fixture.Value("images"));
        Assert.Empty(fixture.MediaService.Invocations);

        if (import is ImportMediaFromBase64 base64Import)
            base64Import.Base64 = "BAUG";
        else if (import is ImportMediaFromBytes bytesImport)
            bytesImport.Bytes = [4, 5, 6];
        Assert.Equal(comparer, Assert.Single(fixture.References([import], singleMedia: true).Comparers));

        import.SortOrder = 8;
        Assert.NotEqual(comparer, Assert.Single(fixture.References([import], singleMedia: true).Comparers));
        Assert.Equal(fixture.Hash(import, excluded, "SortOrder"),
            Assert.Single(fixture.References([import], singleMedia: false).Comparers));
    }

    [Fact]
    public void ExternalUrlComputedIdentityIgnoresActionAndSortOrderButNotUrlOrFilename()
    {
        var fixture = new Fixture();
        var import = External("", "unused", 2);
        import.Comparer = null;
        var comparer = fixture.Hash(import, "Action", "SortOrder");
        var existing = fixture.Media("", comparer, 2);
        fixture.Picker("images", existing);

        import.SortOrder = 9;
        Assert.Equal(comparer, Assert.Single(fixture.References([import], singleMedia: true).Comparers));
        Assert.False(fixture.Import([import]));
        Assert.Equal(Udi(existing), fixture.Value("images"));
        fixture.MediaService.Verify(x => x.Save(existing.Object, -1), Times.Once);

        import.Action = ImportMediaAction.Delete;
        Assert.Equal(comparer, Assert.Single(fixture.References([import], singleMedia: true).Comparers));
        Assert.True(fixture.Import([import]));
        Assert.Equal("", fixture.Value("images"));

        import.Url = "https://example.invalid/changed.png";
        Assert.NotEqual(comparer, Assert.Single(fixture.References([import], singleMedia: true).Comparers));
        import.Url = "https://example.invalid/media.png";
        import.FileName = "renamed.png";
        Assert.NotEqual(comparer, Assert.Single(fixture.References([import], singleMedia: true).Comparers));
    }

    [Fact]
    public void EqualFilenamesDoNotCollapseDifferentMediaIdentities()
    {
        var fixture = new Fixture();
        var first = fixture.Media("first", "first-comparer");
        var second = fixture.Media("second", "second-comparer");

        Assert.True(fixture.Import([External("first", "first-comparer"), External("second", "second-comparer")]));

        Assert.Equal(string.Join(",", Udi(first), Udi(second)), fixture.Value("images"));
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Fact]
    public void MediaReferencesCollectIdentifiersComparersAndDistinctUdiKeys()
    {
        var fixture = new Fixture();
        var key = Guid.NewGuid();
        var external = External("identity", "explicit", 10);
        var references = fixture.References(
        [
            external,
            new ImportMediaFromUdi { Udi = "umb://media/" + key.ToString("N") },
            new ImportMediaFromUdi { Udi = "umb://media/" + key.ToString("N") },
            new ImportMediaFromUdi { Udi = "invalid" },
        ], singleMedia: true);

        Assert.Equal(new[] { "identity" }, references.Identifiers);
        Assert.Equal(new[] { "explicit" }, references.Comparers);
        Assert.Equal(new[] { key }, references.Keys);
    }

    [Fact]
    public void ReplacementKeepsOnlyIncomingReferencesSortedDeduplicatedAndReused()
    {
        var fixture = new Fixture();
        var removed = fixture.Media("removed", "old", 1);
        var last = fixture.Media("last", "last", 30);
        var first = fixture.Media("first", "first", 10);
        fixture.Picker("images", removed, last);

        Assert.True(fixture.Import([
            External("last", "last", 20),
            new ImportMediaFromUdi { Udi = Udi(first) },
            new ImportMediaFromUdi { Udi = Udi(last) },
            External("first", "first", 10),
        ], replaceExisting: true));

        Assert.Equal(string.Join(",", Udi(first), Udi(last)), fixture.Value("images"));
        fixture.MediaService.Verify(x => x.Save(last.Object, -1), Times.Once);
        Assert.Equal(20, last.Object.GetValue<int>("ekmSortOrder"));
        Assert.DoesNotContain(fixture.MediaService.Invocations, x => x.Method.Name.StartsWith("Create", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReplacementReusesBinaryMediaWithExistingSingleMediaHashRules(bool base64)
    {
        var fixture = new Fixture();
        var original = fixture.Media("original", "original");
        fixture.Picker("images", original);
        IImportMedia incoming = base64
            ? new ImportMediaFromBase64 { Base64 = "AQID", FileName = "media.png", NodeName = "Media", Identifier = "", SortOrder = 7 }
            : new ImportMediaFromBytes { Bytes = [1, 2, 3], FileName = "media.png", NodeName = "Media", Identifier = "", SortOrder = 7 };
        var comparer = fixture.Hash(incoming, base64 ? "Base64" : "Bytes");
        Assert.NotEqual(comparer, fixture.Hash(incoming, base64 ? "Base64" : "Bytes", "SortOrder"));
        var reused = fixture.Media("", comparer);

        Assert.True(fixture.Import([incoming, incoming], replaceExisting: true));

        Assert.Equal(Udi(reused), fixture.Value("images"));
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyReplacementClearsCsvAndJsonPickersAndRepeatedEmptyIsNoOp(bool json)
    {
        var fixture = new Fixture();
        var existing = fixture.Media("existing", "existing");
        fixture.SetPicker("images", PickerValue(json, existing));

        Assert.True(fixture.Import([], replaceExisting: true));
        Assert.Equal("", fixture.Value("images"));
        fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Once);

        fixture.Content.Invocations.Clear();
        fixture.ContentService.Invocations.Clear();
        Assert.False(fixture.Import([], replaceExisting: true));
        fixture.AssertPickerUnchanged("");
    }

    [Theory]
    [InlineData("[invalid-json")]
    [InlineData("[]")]
    [InlineData("[{\"mediaKey\":null}]")]
    [InlineData(",,,")]
    [InlineData("  ")]
    public void EmptyReplacementClearsUnresolvableOrEmptyFieldValues(string raw)
    {
        var fixture = new Fixture();
        fixture.SetPicker("images", raw);

        Assert.True(fixture.Import([], replaceExisting: true));

        Assert.Equal("", fixture.Value("images"));
        fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Once);
    }

    [Fact]
    public void UnchangedJsonReplacementDoesNotRewriteTheField()
    {
        var fixture = new Fixture();
        var media = fixture.Media("identity", "same");
        var raw = PickerValue(true, media);
        fixture.SetPicker("images", raw);

        Assert.False(fixture.Import([new ImportMediaFromUdi { Udi = Udi(media) }], replaceExisting: true));

        fixture.AssertPickerUnchanged(raw);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("missing")]
    [InlineData("outside-root")]
    [InlineData("wrong-type")]
    public void ReplacementRejectsUdisNotResolvedAsExpectedTypeInCurrentIndex(string scenario)
    {
        var fixture = new Fixture(published: true);
        var existing = fixture.Media("existing", "existing");
        fixture.Picker("images", existing);
        var incomingUdi = scenario switch
        {
            "invalid" => "not-a-udi",
            "wrong-type" => Udi(fixture.Media("file", "file", mediaType: ImportMediaTypes.File)),
            _ => "umb://media/" + Guid.NewGuid().ToString("N"),
        };
        if (scenario == "outside-root")
        {
            // Globally resolvable media must not bypass the root-limited index supplied to the import.
            var outside = new Mock<IMedia>();
            outside.SetupGet(x => x.Key).Returns(Guid.Parse(incomingUdi["umb://media/".Length..]));
            outside.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(x => x.Alias == "Image"));
            fixture.MediaService.Setup(x => x.GetById(outside.Object.Key)).Returns(outside.Object);
        }

        Assert.Throws<InvalidOperationException>(() => fixture.Import([
            new ImportMediaFromUdi { Udi = incomingUdi },
        ], replaceExisting: true));

        fixture.AssertPickerUnchanged(Udi(existing));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FailedUrlReplacementLeavesOriginalRawPickerUntouchedAfterEarlierSuccess(bool json, bool matchesExistingIdentifier)
    {
        using var handler = new FailedMediaHandler();
        using var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);
        var fixture = new Fixture(published: true, httpClientFactory: factory.Object);
        var original = fixture.Media("original", "original", 1);
        var reused = fixture.Media("reused", "reused", 30);
        if (matchesExistingIdentifier) fixture.Media("failed", "old-comparer");
        var raw = PickerValue(json, original);
        fixture.SetPicker("images", raw);

        Assert.Throws<InvalidOperationException>(() => fixture.Import([
            External("reused", "reused", 10),
            External("failed", "failed"),
        ], replaceExisting: true));

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(10, reused.Object.GetValue<int>("ekmSortOrder"));
        fixture.MediaService.Verify(x => x.Save(reused.Object, -1), Times.Once);
        fixture.AssertPickerUnchanged(raw);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedBase64ReplacementLeavesOriginalRawPickerUntouchedAfterEarlierSuccess(bool json)
    {
        var fixture = new Fixture(published: true);
        var original = fixture.Media("original", "original");
        var incoming = fixture.Media("incoming", "incoming");
        var raw = PickerValue(json, original);
        fixture.SetPicker("images", raw);

        Assert.Throws<FormatException>(() => fixture.Import([
            new ImportMediaFromUdi { Udi = Udi(incoming) },
            new ImportMediaFromBase64 { Base64 = "not valid base64!", FileName = "media.png", NodeName = "Media", Identifier = "failed" },
        ], replaceExisting: true));

        fixture.AssertPickerUnchanged(raw);
    }

    [Fact]
    public void InvalidBytesReplacementDoesNotMutatePicker()
    {
        var fixture = new Fixture();
        var original = fixture.Media("original", "original");
        fixture.Picker("images", original);

        Assert.Throws<ArgumentNullException>(() => fixture.Import([
            new ImportMediaFromBytes { Bytes = null!, FileName = "media.png", NodeName = "Media", Identifier = "failed" },
        ], replaceExisting: true));

        fixture.AssertPickerUnchanged(Udi(original));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("unsupported")]
    [InlineData("delete")]
    [InlineData("invalid-action")]
    public void ReplacementPrevalidatesAllElementsBeforeImportingCandidates(string scenario)
    {
        var fixture = new Fixture(published: true);
        var original = fixture.Media("original", "original");
        var candidate = fixture.Media("candidate", "candidate", 30);
        fixture.Picker("images", original);
        IImportMedia invalid = scenario switch
        {
            "null" => null!,
            "unsupported" => new UnsupportedImportMedia(),
            "delete" => new ImportMediaFromUdi { Udi = Udi(original), Action = ImportMediaAction.Delete },
            _ => External("original", "original", action: (ImportMediaAction)999),
        };

        Assert.Throws<ArgumentException>(() => fixture.Import([
            External("candidate", "candidate", 10), invalid,
        ], replaceExisting: true));

        Assert.Equal(30, candidate.Object.GetValue<int>("ekmSortOrder"));
        Assert.Empty(fixture.MediaService.Invocations);
        fixture.AssertPickerUnchanged(Udi(original));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulReplacementPreservesPublicationStatus(bool published)
    {
        var fixture = new Fixture(published);
        var original = fixture.Media("original", "original");
        var incoming = fixture.Media("incoming", "incoming");
        fixture.Picker("images", original);

        Assert.True(fixture.Import([new ImportMediaFromUdi { Udi = Udi(incoming) }], replaceExisting: true));

        Assert.Equal(Udi(incoming), fixture.Value("images"));
        var publish = fixture.ContentService.Invocations.Where(x => x.Method.Name.Contains("Publish", StringComparison.Ordinal)).ToList();
        if (published)
        {
            Assert.Equal(Fixture.SyncUser, Assert.Single(publish).Arguments.Last());
#if NET8_0
            fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Never);
#else
            fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Once);
#endif
        }
        else
        {
            Assert.Empty(publish);
            fixture.ContentService.Verify(x => x.Save(fixture.Content.Object, Fixture.SyncUser, null), Times.Once);
        }
    }

    [Fact]
    public void FileReplacementDoesNotChangeImagesOrDeleteMediaFiles()
    {
        var fixture = new Fixture();
        var image = fixture.Media("image", "image");
        var original = fixture.Media("original", "original", mediaType: ImportMediaTypes.File);
        var incoming = fixture.Media("incoming", "incoming", mediaType: ImportMediaTypes.File);
        fixture.Picker("images", image);
        fixture.Picker("files", original);

        Assert.True(fixture.Import([new ImportMediaFromUdi { Udi = Udi(incoming) }],
            ImportMediaTypes.File, ImportMediaContentTypes.files, replaceExisting: true));

        Assert.Equal(Udi(image), fixture.Value("images"));
        Assert.Equal(Udi(incoming), fixture.Value("files"));
        Assert.Empty(fixture.MediaService.Invocations);
    }

    [Fact]
    public void UnchangedReplacementDoesNotSetValueSaveOrPublish()
    {
        var fixture = new Fixture(published: true);
        var media = fixture.Media("identity", "same", 2);
        fixture.Picker("images", media);

        Assert.False(fixture.Import([External("identity", "same", 2)], replaceExisting: true));

        fixture.AssertPickerUnchanged(Udi(media));
        Assert.Empty(fixture.MediaService.Invocations);
    }

    private static string PickerValue(bool json, Mock<IMedia> media)
        => json ? JsonSerializer.Serialize(new[] { new { key = Guid.NewGuid(), mediaKey = media.Object.Key } }) : Udi(media);

    private sealed class UnsupportedImportMedia : IImportMedia
    {
        public int? SortOrder { get; set; }
        public ImportMediaAction Action { get; set; } = ImportMediaAction.Add;
    }

    private sealed class FailedMediaHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static ImportMediaFromExternalUrl External(string identifier, string comparer, int? sortOrder = null,
        ImportMediaAction action = ImportMediaAction.Add)
        => new()
        {
            Url = "https://example.invalid/media.png",
            FileName = "media.png",
            NodeName = "Media",
            Identifier = identifier,
            Comparer = comparer,
            SortOrder = sortOrder,
            Action = action,
        };

    private static string Udi(Mock<IMedia> media) => "umb://media/" + media.Object.Key.ToString("N");

    private sealed class Fixture
    {
        public const int SyncUser = 42;
        private readonly ImportService _service;
        private readonly List<IMedia> _media = [];
        private readonly Dictionary<string, string> _pickers = new();
        public Mock<IContent> Content { get; } = new();
        public Mock<IContentService> ContentService { get; } = new();
        public Mock<IMediaService> MediaService { get; } = new();

        public Fixture(bool published = false, IHttpClientFactory? httpClientFactory = null)
        {
            Content.SetupGet(x => x.Published).Returns(published);
            Content.Setup(x => x.GetValue<string>(It.IsAny<string>(), null, null, false))
                .Returns((string alias, string? culture, string? segment, bool publishedValue) => Value(alias));
            Content.Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object>(), null, null))
                .Callback((string alias, object value, string? culture, string? segment) => _pickers[alias] = (string)value);
            var mediaImporter = new ImportMediaService(MediaService.Object, null!, httpClientFactory!, null!, null!, null!, null!,
                NullLogger<ImportMediaService>.Instance, null!);
#if NET8_0
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, mediaImporter, null!);
#else
            _service = new ImportService(null!, ContentService.Object, null!, null!, null!,
                NullLogger<ImportService>.Instance, null!, null!, mediaImporter, null!);
#endif
        }

        public Mock<IMedia> Media(string identifier, string comparer, int? sortOrder = null,
            ImportMediaTypes mediaType = ImportMediaTypes.Image)
        {
            var media = new Mock<IMedia>();
            var values = new Dictionary<string, object> { ["ekmIdentifier"] = identifier, ["comparer"] = comparer };
            if (sortOrder.HasValue) values["ekmSortOrder"] = sortOrder.Value;
            media.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            media.SetupGet(x => x.ContentType).Returns(Mock.Of<ISimpleContentType>(x => x.Alias == mediaType.ToString()));
            media.Setup(x => x.HasProperty(It.IsAny<string>())).Returns((string alias) => values.ContainsKey(alias));
            media.Setup(x => x.GetValue<string>(It.IsAny<string>(), null, null, false))
                .Returns((string alias, string? culture, string? segment, bool publishedValue) =>
                    values.TryGetValue(alias, out var value) ? value.ToString() : null);
            media.Setup(x => x.GetValue<int>(It.IsAny<string>(), null, null, false))
                .Returns((string alias, string? culture, string? segment, bool publishedValue) =>
                    values.TryGetValue(alias, out var value) ? Convert.ToInt32(value) : 0);
            media.Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object>(), null, null))
                .Callback((string alias, object value, string? culture, string? segment) => values[alias] = value);
            _media.Add(media.Object);
            return media;
        }

        public string Value(string alias) => _pickers.GetValueOrDefault(alias, "");
        public void SetPicker(string alias, string value) => _pickers[alias] = value;
        public void Picker(string alias, params Mock<IMedia>[] media) => SetPicker(alias, string.Join(",", media.Select(Udi)));

        public bool Import(List<IImportMedia> imports, ImportMediaTypes type = ImportMediaTypes.Image,
            ImportMediaContentTypes picker = ImportMediaContentTypes.images, bool saveContent = true, bool replaceExisting = false)
            => (bool)Invoke("ImportSingleMedia", [Content.Object, imports, _media, null, type, picker, saveContent, SyncUser, replaceExisting])!;

        public void AssertPickerUnchanged(string original)
        {
            Assert.Equal(original, Value("images"));
            Assert.DoesNotContain(Content.Invocations, x => x.Method.Name == "SetValue");
            Assert.Empty(ContentService.Invocations);
        }

        public List<string> CurrentUdis(ImportMediaContentTypes picker)
            => (List<string>)Invoke("GetCurrentMediaUdis", [Content.Object, picker])!;

        public string Hash(IImportMedia media, params string[] excluded)
            => (string)Invoke("ComputeSha256Hash", [media, excluded])!;

        public (HashSet<string> Identifiers, HashSet<string> Comparers, HashSet<Guid> Keys) References(
            List<IImportMedia> imports, bool singleMedia)
        {
            var identifiers = new HashSet<string>();
            var comparers = new HashSet<string>();
            var keys = new HashSet<Guid>();
            Invoke("AddMediaReferences", [imports, identifiers, comparers, keys, singleMedia]);
            return (identifiers, comparers, keys);
        }

        private object? Invoke(string method, object?[] arguments)
        {
            try
            {
                return typeof(ImportService).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_service, arguments);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
