#if !NET8_0
using Ekom.Umb.Services;
using Moq;
using Umbraco.Cms.Core.Models;
using Xunit;

namespace Ekom.Tests.Tests;

public class ImportComparerPropertyTests
{
    private static Mock<IContent> Content(Dictionary<string, object?> values)
    {
        var content = new Mock<IContent>();
        content.Setup(x => x.HasProperty(It.IsAny<string>())).Returns((string alias) => values.ContainsKey(alias));
        content.Setup(x => x.GetValue<string>(It.IsAny<string>(), null, null, false))
            .Returns((string alias, string? culture, string? segment, bool published) =>
                values.TryGetValue(alias, out var value) ? value?.ToString() : null);
        content.Setup(x => x.SetValue(It.IsAny<string>(), It.IsAny<object?>(), null, null))
            .Callback((string alias, object? value, string? culture, string? segment) => values[alias] = value);
        return content;
    }

    [Fact]
    public void TypeWithOnlyTheOldAliasIsReadAndWrittenThere()
    {
        var values = new Dictionary<string, object?> { ["comparer"] = "old" };
        var content = Content(values);

        Assert.Equal("old", ImportComparer.Get(content.Object));
        ImportComparer.Set(content.Object, "new");

        Assert.Equal("new", values["comparer"]);
        Assert.False(values.ContainsKey("ekmComparer"));
    }

    [Fact]
    public void TypeWithTheNewAliasIsReadAndWrittenThere()
    {
        var values = new Dictionary<string, object?> { ["ekmComparer"] = "current" };
        var content = Content(values);

        Assert.Equal("current", ImportComparer.Get(content.Object));
        ImportComparer.Set(content.Object, "new");

        Assert.Equal("new", values["ekmComparer"]);
        Assert.False(values.ContainsKey("comparer"));
    }

    [Fact]
    public void TypeWithBothUsesTheOldValueUntilTheNewOneIsWritten()
    {
        var values = new Dictionary<string, object?> { ["ekmComparer"] = null, ["comparer"] = "old" };
        var content = Content(values);

        // An empty new property must not make an unchanged node look changed.
        Assert.Equal("old", ImportComparer.Get(content.Object));

        ImportComparer.Set(content.Object, "new");

        Assert.Equal("new", values["ekmComparer"]);
        Assert.Equal("old", values["comparer"]);
        Assert.Equal("new", ImportComparer.Get(content.Object));
    }
}
#endif
