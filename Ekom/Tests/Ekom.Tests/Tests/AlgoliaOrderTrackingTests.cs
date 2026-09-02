using Ekom.Models;
using Xunit;

namespace Ekom.Tests.Tests;

public sealed class AlgoliaOrderTrackingTests
{
    [Fact]
    public void AddLine_Preserves_The_First_Query_Id_For_A_Line()
    {
        var lineKey = Guid.NewGuid();
        var tracking = new AlgoliaOrderTracking();

        tracking.AddLine(lineKey, "first-query");
        tracking.AddLine(lineKey, "second-query");

        Assert.Single(tracking.Lines);
        Assert.Equal("first-query", tracking.GetQueryId(lineKey));
    }

    [Fact]
    public void RemoveLine_Removes_The_Associated_Query_Id()
    {
        var lineKey = Guid.NewGuid();
        var tracking = new AlgoliaOrderTracking();
        tracking.AddLine(lineKey, "query-id");

        tracking.RemoveLine(lineKey);

        Assert.Empty(tracking.Lines);
        Assert.Null(tracking.GetQueryId(lineKey));
    }
}
