using Ekom.Models;
using Ekom.Umb.Services;
using Moq;
using System.Globalization;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Xunit;

namespace Ekom.Tests.Tests;

public class UmbracoContentDateTests
{
    private const string Alias = "saleDate";
    private const string DateEditor = "Umbraco.DateTime";
#if NET10_0
    private const string DateJson = "{\"date\":\"2026-10-16T13:14:15.1234567+05:30\",\"timeZone\":\"Asia/Kolkata\"}";
#endif

    public static IEnumerable<object[]> TypedDates()
    {
        foreach (var culture in new[] { "is-IS", "en-US" })
        {
            foreach (var kind in new[] { DateTimeKind.Utc, DateTimeKind.Local, DateTimeKind.Unspecified })
            {
                yield return [culture, new DateTime(2026, 10, 16, 13, 14, 15, kind).AddTicks(1234567)];
            }

            yield return [culture, new DateTimeOffset(2026, 10, 16, 13, 14, 15, TimeSpan.FromHours(5.5)).AddTicks(1234567)];
        }
    }

    [Theory]
    [MemberData(nameof(TypedDates))]
    public void PublishedTypedDatePreservesSourceKindOffsetAndPrecision(string culture, object date)
    {
        using var scope = new CultureScope(culture);
        var property = PublishedProperty(DateEditor, date, new DateTime(2000, 1, 1));

        var cached = Adapt(PublishedContent(property.Object));

        Assert.Equal(RoundTrip(date), cached.Properties[Alias]);
    }

    [Theory]
    [MemberData(nameof(TypedDates))]
    public void EditableTypedDatePreservesKindOffsetAndPrecision(string culture, object date)
    {
        using var scope = new CultureScope(culture);
        var content = EditableContent(DateEditor, date, "culture-dependent fallback");

        var cached = Adapt(content.Object);

        Assert.Equal(RoundTrip(date), cached.Properties[Alias]);
        content.Verify(x => x.GetValue<string>(Alias, null, null, false), Times.Never);
    }

    [Theory]
    [InlineData("is-IS", false)]
    [InlineData("en-US", false)]
    [InlineData("is-IS", true)]
    [InlineData("en-US", true)]
    public void PublishedStringDateUsesConvertedValueAndExistingFirstCulture(string culture, bool variesByCulture)
    {
        using var scope = new CultureScope(culture);
        var date = new DateTime(2026, 10, 16, 13, 14, 15, DateTimeKind.Unspecified).AddTicks(1234567);
        var propertyCulture = variesByCulture ? "is-IS" : null;
        var property = PublishedProperty(DateEditor, "16.10.2026 13:14:15", date, variesByCulture, propertyCulture);

        var cached = Adapt(PublishedContent(property.Object));

        Assert.Equal(RoundTrip(date), cached.Properties[Alias]);
        property.Verify(x => x.GetSourceValue(propertyCulture, null), Times.Once);
        property.Verify(x => x.GetValue(propertyCulture, null), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    public void PublishedMissingOrInvalidDateDoesNotBecomeMinValue(string? source)
    {
        var property = PublishedProperty(DateEditor, source, DateTime.MinValue);

        var cached = Adapt(PublishedContent(property.Object));

        Assert.Equal(source ?? string.Empty, cached.Properties[Alias]);
        if (string.IsNullOrWhiteSpace(source))
        {
            property.Verify(x => x.GetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    [Fact]
    public void PublishedDateWithoutValueDoesNotUseConvertedSentinel()
    {
        var property = PublishedProperty(DateEditor, "invalid", DateTime.MinValue);
        property.Setup(x => x.HasValue(null, null)).Returns(false);

        Assert.Equal("invalid", Adapt(PublishedContent(property.Object)).Properties[Alias]);
        property.Verify(x => x.GetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void StoredTypedMinValueIsNotTreatedAsAConverterSentinel()
    {
        var property = PublishedProperty(DateEditor, DateTime.MinValue, null);

        Assert.Equal(RoundTrip(DateTime.MinValue), Adapt(PublishedContent(property.Object)).Properties[Alias]);
        Assert.Equal(RoundTrip(DateTime.MinValue),
            Adapt(EditableContent(DateEditor, DateTime.MinValue, null).Object).Properties[Alias]);
    }

    [Theory]
    [InlineData("is-IS")]
    [InlineData("en-US")]
    public void NonDateEditorsKeepSourceAndExistingStringConversion(string culture)
    {
        using var scope = new CultureScope(culture);
        var date = new DateTime(2026, 10, 16, 13, 14, 15, DateTimeKind.Utc);
        var text = PublishedProperty("Umbraco.TextBox", "original text", date);
        var boxed = PublishedProperty("Umbraco.TextBox", date, date);
        var editable = EditableContent("Umbraco.TextBox", date, "existing generic string conversion");

        Assert.Equal("original text", Adapt(PublishedContent(text.Object)).Properties[Alias]);
        Assert.Equal(date.ToString(), Adapt(PublishedContent(boxed.Object)).Properties[Alias]);
        Assert.Equal("existing generic string conversion", Adapt(editable.Object).Properties[Alias]);
        text.Verify(x => x.GetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        boxed.Verify(x => x.GetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        editable.Verify(x => x.GetValue<string>(Alias, null, null, false), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ordinary text")]
    public void EditableEmptyDatesAndPlainTextKeepExistingValues(string? value)
    {
        Assert.Equal(value ?? string.Empty, Adapt(EditableContent(DateEditor, value, value).Object).Properties[Alias]);
        Assert.Equal(value ?? string.Empty, Adapt(EditableContent("Umbraco.TextBox", value, value).Object).Properties[Alias]);
    }

#if NET10_0
    [Theory]
    [InlineData("is-IS", "Umbraco.DateTimeUnspecified", "2026-10-16T13:14:15.1234567")]
    [InlineData("en-US", "Umbraco.DateTimeUnspecified", "2026-10-16T13:14:15.1234567")]
    [InlineData("is-IS", "Umbraco.DateTimeWithTimeZone", "2026-10-16T13:14:15.1234567+05:30")]
    [InlineData("en-US", "Umbraco.DateTimeWithTimeZone", "2026-10-16T13:14:15.1234567+05:30")]
    public void ModernDateEditorsNormalizeEditableJsonAndPublishedConvertedValue(string culture, string editor, string expected)
    {
        using var scope = new CultureScope(culture);
        var offset = new DateTimeOffset(2026, 10, 16, 13, 14, 15, TimeSpan.FromHours(5.5)).AddTicks(1234567);
        object converted = editor == "Umbraco.DateTimeUnspecified" ? (object)offset.DateTime : offset;
        var property = PublishedProperty(editor, DateJson, converted);

        Assert.Equal(expected, Adapt(PublishedContent(property.Object)).Properties[Alias]);
        Assert.Equal(expected, Adapt(EditableContent(editor, DateJson, DateJson).Object).Properties[Alias]);
    }

    [Theory]
    [InlineData("2026-07-01T12:00:00Z", "2026-07-01T12:00:00.0000000+00:00")]
    [InlineData("2026-07-01T12:00:00-04:00", "2026-07-01T12:00:00.0000000-04:00")]
    [InlineData("2026-07-01T12:00:00", null)]
    [InlineData("2026-07-01", null)]
    [InlineData("not a date", null)]
    public void TimezoneEditorRequiresAnExplicitStoredOffset(string date, string? expected)
    {
        var source = System.Text.Json.JsonSerializer.Serialize(new { date });

        Assert.Equal(expected ?? source,
            Adapt(EditableContent("Umbraco.DateTimeWithTimeZone", source, source).Object).Properties[Alias]);

        if (expected == null)
        {
            var property = PublishedProperty("Umbraco.DateTimeWithTimeZone", source,
                new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(2)));
            Assert.Equal(source, Adapt(PublishedContent(property.Object)).Properties[Alias]);
            property.Verify(x => x.GetValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"date\":null}")]
    [InlineData("{\"date\":123}")]
    public void InvalidModernDateStorageIsPreserved(string source)
    {
        Assert.Equal(source,
            Adapt(EditableContent("Umbraco.DateTimeUnspecified", source, source).Object).Properties[Alias]);
    }

    [Theory]
    [InlineData("Umbraco.DateOnly")]
    [InlineData("Umbraco.TimeOnly")]
    public void DateOnlyAndTimeOnlyEditorsKeepJsonUnchanged(string editor)
    {
        var property = PublishedProperty(editor, DateJson, new DateTime(2026, 10, 16));

        Assert.Equal(DateJson, Adapt(PublishedContent(property.Object)).Properties[Alias]);
        Assert.Equal(DateJson, Adapt(EditableContent(editor, DateJson, DateJson).Object).Properties[Alias]);
    }
#endif

    private static Mock<IPublishedProperty> PublishedProperty(
        string editor, object? source, object? converted, bool variesByCulture = false, string? culture = null)
    {
        var type = new Mock<IPublishedPropertyType>();
        type.SetupGet(x => x.EditorAlias).Returns(editor);
#if NET8_0
        type.SetupGet(x => x.DataType).Returns(new PublishedDataType(1, editor, new Lazy<object?>(() => null)));
#else
        type.SetupGet(x => x.DataType).Returns(new PublishedDataType(1, editor, editor, new Lazy<object?>(() => null)));
#endif
        type.SetupGet(x => x.Variations).Returns(variesByCulture ? ContentVariation.Culture : ContentVariation.Nothing);
        var property = new Mock<IPublishedProperty>();
        property.SetupGet(x => x.Alias).Returns(Alias);
        property.SetupGet(x => x.PropertyType).Returns(type.Object);
        property.Setup(x => x.GetSourceValue(culture, null)).Returns(source);
        property.Setup(x => x.HasValue(culture, null)).Returns(true);
        property.Setup(x => x.GetValue(culture, null)).Returns(converted);
        return property;
    }

    private static IPublishedContent PublishedContent(IPublishedProperty property)
    {
        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.Properties).Returns([property]);
        content.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>
        {
            ["is-IS"] = new("is-IS", "Item", "item", new DateTime(2026, 10, 16)),
            ["en-US"] = new("en-US", "Item", "item", new DateTime(2026, 10, 16)),
        });
        return content.Object;
    }

    private static Mock<IContent> EditableContent(string editor, object? value, string? stringValue)
    {
        var type = new Mock<IPropertyType>();
        type.SetupGet(x => x.PropertyEditorAlias).Returns(editor);
        var property = new Mock<IProperty>();
        property.SetupGet(x => x.Alias).Returns(Alias);
        property.SetupGet(x => x.PropertyType).Returns(type.Object);
        var content = new Mock<IContent>();
        content.SetupGet(x => x.AvailableCultures).Returns(Array.Empty<string>());
        content.SetupGet(x => x.Properties).Returns(new PropertyCollection([property.Object]));
        content.Setup(x => x.GetValue(Alias, null, null, false)).Returns(value);
        content.Setup(x => x.GetValue<string>(Alias, null, null, false)).Returns(stringValue);
        return content;
    }

    private static UmbracoContent Adapt(object content)
    {
#if NET8_0
        const string typeName = "Ekom.Umb.Models.Umbraco10Content";
#else
        const string typeName = "Ekom.Umb.Models.Umbraco17Content";
#endif
        var type = typeof(ImportService).Assembly.GetType(typeName, throwOnError: true)!;
        var constructor = Assert.Single(type.GetConstructors(), x => x.GetParameters()[0].ParameterType.IsInstanceOfType(content));
        var arguments = constructor.GetParameters()
            .Select((parameter, index) => index == 0 ? content : parameter.HasDefaultValue ? parameter.DefaultValue : Guid.Empty)
            .ToArray();
        return (UmbracoContent)constructor.Invoke(arguments);
    }

    private static string RoundTrip(object date) => date switch
    {
        DateTime value => value.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset value => value.ToString("O", CultureInfo.InvariantCulture),
        _ => throw new ArgumentException("Expected a typed date.", nameof(date)),
    };

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _original = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUi = CultureInfo.CurrentUICulture;

        public CultureScope(string culture)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _original;
            CultureInfo.CurrentUICulture = _originalUi;
        }
    }
}
