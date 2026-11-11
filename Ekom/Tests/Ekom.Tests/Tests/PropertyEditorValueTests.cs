using Ekom.Utilities;
using Xunit;

namespace Ekom.Tests.Tests;

public class PropertyEditorValueTests
{
    private static readonly string[] Aliases = ["IS", "is", "EN", "is-IS", "en-US", "", "values", "type", "markup", "missing", "Web"];

    public static IEnumerable<object[]> Values()
    {
        string[] values =
        [
            "plain text",
            "{\"values\":{\"IS\":\"Vara\",\"EN\":\"Product\"},\"type\":\"Store\"}",
            "{\"Values\":{\"is\":\"lower\",\"EN\":\"Product\"}}",
            "{\"values\":{\"IS\":\"a\",\"is\":\"b\"}}",
            "{\"values\":{\"IS\":\"a\",\"IS\":\"b\"}}",
            "{\"values\":{\"EN\":\"first\",\"EN\":\"second\",\"IS\":\"x\"}}",
            "{\"values\":{\"is-IS\":\"Greiðsla\",\"en-US\":\"Payment\"},\"type\":\"Language\"}",
            "{\"values\":{}}",
            "{\"values\":null}",
            "{\"values\":\"text\"}",
            "{\"values\":[1,2]}",
            "{\"values\":{\"IS\":null,\"EN\":\"Product\"}}",
            "{\"values\":{\"IS\":12.50,\"EN\":true}}",
            "{\"values\":{\"IS\":1e3,\"EN\":100000000000000000000}}",
            "{\"values\":{\"IS\":\"[{\\\"Currency\\\":\\\"is-IS\\\",\\\"Price\\\":1990}]\"}}",
            "{\"values\":{\"IS\":{\"markup\":\"<p>Halló</p>\",\"blocks\":{}},\"EN\":{\"other\":1}}}",
            "{\"values\":{\"IS\":[\"a\",\"b\"]}}",
            "{\"values\":{\"IS\":\"2024-01-01T00:00:00Z\",\"EN\":\"2024-01-01T00:00:00\"}}",
            "{\"values\":{\"IS\":\"/Date(1700000000000)/\",\"EN\":\"/path/to\"}}",
            "{\"values\":{\"IS\":\"2024-01-01\",\"EN\":\"1234567890T234567890\"}}",
            "{\"values\":{\"IS\":\"line\\nbreak \\u00e9 \\\"quoted\\\" &amp; <b>\"}}",
            "{\"values\":{\"IS\":\"\"}}",
            "{\"IS\":\"direct\",\"values\":{\"IS\":\"nested\"}}",
            "{\"IS\":null,\"values\":{\"IS\":\"nested\"}}",
            "{\"IS\":{\"markup\":\"<p>direct</p>\"}}",
            "{\"IS\":5,\"EN\":false}",
            "{\"is\":\"lower\",\"IS\":\"upper\"}",
            "{\"\":\"empty key\"}",
            "{\"markup\":\"<p>rte</p>\",\"blocks\":{}}",
            "{\"type\":\"Store\"}",
            "{}",
            "{ \"values\" : { \"IS\" : \"spaced\" } }  ",
            "{'values':{'IS':'single quotes'}}",
            "{\"values\":{\"IS\":\"trailing\",},}",
            "{/* comment */\"values\":{\"IS\":\"commented\"}}",
            "{values:{IS:\"unquoted\"}}",
            "{\"values\":{\"IS\":\"broken\"}",
            "{not json at all}",
            "[{\"Currency\":\"is-IS\",\"Price\":1990}]",
            "[1,2,3]",
            " [\"udi\"] ",
            "[broken",
            "[not, json]",
            "[]",
        ];

        return values.Select(x => new object[] { x });
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void FastPath_MatchesJTokenParser(string value)
    {
        foreach (var alias in Aliases)
        {
            foreach (var fallback in new[] { false, true })
            {
                var expected = StringExtension.GetEkomPropertyEditorValueWithJToken(value, alias, fallback);

                Assert.Equal(expected, value.GetEkomPropertyEditorValue(alias, fallback));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsSerializedRichTextFromSelectedEntry(bool direct)
    {
        const string markup = "<p>Endurn\u00e6r\u00f0u &nbsp;</p>";
        var richText = System.Text.Json.JsonSerializer.Serialize(new { markup, blocks = new { contentData = Array.Empty<object>() } });
        var entries = new Dictionary<string, string> { ["IS"] = richText };
        var value = direct
            ? System.Text.Json.JsonSerializer.Serialize(entries)
            : System.Text.Json.JsonSerializer.Serialize(new { values = entries });

        Assert.Equal(markup, value.GetEkomPropertyEditorValue("is"));
        Assert.Equal(markup, StringExtension.GetEkomPropertyEditorValueWithJToken(value, "is"));
        if (!direct)
            Assert.Equal(markup, value.GetEkomPropertyEditorValue("missing", fallback: true));
    }

    [Theory]
    [InlineData("{not json}")]
    [InlineData("{\"other\":\"value\"}")]
    [InlineData("{\"markup\":42}")]
    [InlineData("<p>Legacy HTML</p>")]
    public void PreservesStringsThatAreNotRichTextObjects(string text)
    {
        var value = System.Text.Json.JsonSerializer.Serialize(new { values = new Dictionary<string, string> { ["IS"] = text } });
        Assert.Equal(text, value.GetEkomPropertyEditorValue("IS"));
        Assert.Equal(text, StringExtension.GetEkomPropertyEditorValueWithJToken(value, "IS"));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("outlet")]
    public void UnwrappedRichTextIsDefaultForAnyStore(string storeAlias)
    {
        const string markup = "<p>Original content</p>";
        var value = System.Text.Json.JsonSerializer.Serialize(new { markup, blocks = new { contentData = Array.Empty<object>() } });
        Assert.Equal(markup, value.GetEkomPropertyEditorValue(storeAlias));
        Assert.Equal(markup, StringExtension.GetEkomPropertyEditorValueWithJToken(value, storeAlias));
    }

    [Fact]
    public void ReadsStoreValue()
    {
        Assert.Equal("Vara", "{\"values\":{\"IS\":\"Vara\",\"EN\":\"Product\"},\"type\":\"Store\"}".GetEkomPropertyEditorValue("is"));
    }

    [Fact]
    public void FallbackReturnsFirstValueWhenAliasAndCultureAreMissing()
    {
        const string value = "{\"values\":{\"zz-ZZ\":\"first\",\"yy-YY\":\"second\"}}";

        Assert.Equal(string.Empty, value.GetEkomPropertyEditorValue("missing"));
        Assert.Equal("first", value.GetEkomPropertyEditorValue("missing", fallback: true));
    }
}
