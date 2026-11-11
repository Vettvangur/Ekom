#if NET10_0
using Ekom.Cache;
using Ekom.Models;
using Ekom.Umb;
using Moq;
using System.Collections.Concurrent;
using System.Text.Json;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Ekom.Tests.Tests;

public class RichTextSavingNotificationTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task SaveNormalizesUneditedRichTextOnlyForStoreProperties(bool useLanguages, bool cancelled, bool expectedWrite)
    {
        const string raw = "{\"markup\":\"<p>Original</p>\",\"blocks\":{\"contentData\":[{\"key\":\"keep\"}]}}";
        var wrapperKey = Guid.NewGuid();
        var wrappedKey = Guid.NewGuid();
        var dataType = new Mock<IDataType>();
        dataType.SetupGet(x => x.Key).Returns(wrapperKey);
        dataType.SetupGet(x => x.ConfigurationData).Returns(new Dictionary<string, object>
        {
            ["useLanguages"] = useLanguages,
            ["dataType"] = new { guid = wrappedKey },
        });
        var service = new Mock<IDataTypeService>();
        service.Setup(x => x.GetDataType(123)).Returns(dataType.Object);
        service.Setup(x => x.GetAsync(wrappedKey)).ReturnsAsync(Mock.Of<IDataType>(x => x.EditorAlias == "Umbraco.RichText"));
        var type = Mock.Of<IPropertyType>(x => x.PropertyEditorAlias == "Ekom.Property" && x.DataTypeId == 123);
        var property = new Mock<IProperty>();
        property.SetupGet(x => x.Alias).Returns("description");
        property.SetupGet(x => x.PropertyType).Returns(type);
        property.SetupGet(x => x.Values).Returns(new List<IPropertyValue>
        {
            Mock.Of<IPropertyValue>(x => x.EditedValue == (object)raw && x.Culture == "is-IS" && x.Segment == "segment"),
        });
        var content = new Mock<IContent>();
        content.SetupGet(x => x.Properties).Returns(new PropertyCollection([property.Object]));
        string? saved = null;
        content.Setup(x => x.SetValue("description", It.IsAny<object>(), "is-IS", "segment"))
            .Callback<string, object, string, string>((_, value, _, _) => saved = value.ToString());
        var cache = new Mock<IBaseCache<IStore>>();
        cache.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<Guid, IStore>(new Dictionary<Guid, IStore>
        {
            [Guid.NewGuid()] = Mock.Of<IStore>(x => x.Alias == "main"),
            [Guid.NewGuid()] = Mock.Of<IStore>(x => x.Alias == "outlet"),
        }));
        var notification = new ContentSavingNotification([content.Object], new EventMessages());
        if (cancelled) notification.CancelOperation(new EventMessage("Test", "Cancelled", EventMessageType.Error));
        await new RichTextSavingNotificationHandler(service.Object, cache.Object).HandleAsync(notification, CancellationToken.None);
        if (!expectedWrite)
        {
            Assert.Null(saved);
            return;
        }
        using var json = JsonDocument.Parse(Assert.IsType<string>(saved));
        Assert.Equal(wrapperKey, json.RootElement.GetProperty("dtdGuid").GetGuid());
        Assert.Equal("Store", json.RootElement.GetProperty("type").GetString());
        foreach (var alias in new[] { "main", "outlet" })
        {
            var value = json.RootElement.GetProperty("values").GetProperty(alias);
            Assert.Equal("<p>Original</p>", value.GetProperty("markup").GetString());
            Assert.Equal("keep", value.GetProperty("blocks").GetProperty("contentData")[0].GetProperty("key").GetString());
        }
        Assert.Null(RichTextSavingNotificationHandler.Wrap(saved, wrapperKey, ["main", "outlet"]));
    }

    [Theory]
    [InlineData("{\"values\":{\"main\":{\"markup\":\"\"}}}")]
    [InlineData("{broken")]
    [InlineData("{\"markup\":42}")]
    [InlineData("[]")]
    [InlineData(null)]
    public void PreservesWrappedOrInvalidValues(string? raw)
        => Assert.Null(RichTextSavingNotificationHandler.Wrap(raw, Guid.NewGuid(), ["main"]));

    [Fact]
    public void NoStoresDoesNotReplaceExistingContent()
        => Assert.Null(RichTextSavingNotificationHandler.Wrap("<p>Keep</p>", Guid.NewGuid(), []));
}
#endif
