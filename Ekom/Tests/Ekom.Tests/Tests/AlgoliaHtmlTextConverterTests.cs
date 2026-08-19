using Ekom.Algolia;
using Ekom.Algolia.Indexing;
using Ekom.Algolia.Mappers;
using Xunit;

namespace Ekom.Tests.Tests;

public class AlgoliaHtmlTextConverterTests
{
    [Fact]
    public void Parses_StripHtml_Content_Field_Transform()
    {
        var field = AlgoliaContentIndexExecutor.ParseConfiguredField("body|STRIPHTML");

        Assert.Equal("body", field.Alias);
        Assert.Equal(AlgoliaContentFieldTransform.StripHtml, field.Transform);
    }

    [Fact]
    public void Applies_StripHtml_Content_Field_Transform()
    {
        var result = AlgoliaContentIndexExecutor.ApplyConfiguredTransform(
            "{\"markup\":\"<p>Content <strong>body</strong></p>\"}",
            AlgoliaContentFieldTransform.StripHtml);

        Assert.Equal("Content body", result);
    }

    [Fact]
    public void Converts_Html_To_Normalized_Plain_Text()
    {
        const string html = "<p>Hello&nbsp;<strong>world</strong></p><p>Next<br>line</p><script>alert('ignored')</script>";

        var result = AlgoliaHtmlTextConverter.ConvertToText(html);

        Assert.Equal("Hello world Next line", result);
    }

    [Fact]
    public void Converts_Rich_Text_Json_Markup()
    {
        const string value = "{\"markup\":\"<div>Halló &amp; heimur</div>\"}";

        var result = AlgoliaHtmlTextConverter.ConvertToText(value);

        Assert.Equal("Halló & heimur", result);
    }

    [Fact]
    public void Tolerates_Malformed_Html_And_Removes_Non_Content_Nodes()
    {
        const string html = "<p>First <strong>second<p>Third<style>.hidden { display: none; }</style><!-- ignored -->";

        var result = AlgoliaHtmlTextConverter.ConvertToText(html);

        Assert.Equal("First second Third", result);
    }

    [Fact]
    public void Leaves_Non_String_Values_Unchanged()
    {
        var value = new object();

        var result = AlgoliaHtmlTextConverter.Convert(value);

        Assert.Same(value, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>alert('ignored')</script>")]
    public void Returns_Empty_Text_When_No_Indexable_Content_Remains(string value)
    {
        var result = AlgoliaHtmlTextConverter.ConvertToText(value);

        Assert.Equal(string.Empty, result);
    }
}
