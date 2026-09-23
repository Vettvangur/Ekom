using Ekom.Utilities;
using Xunit;

namespace Ekom.Tests.Tests;

public class ImportSyncProgressTrackerTests
{
    [Fact]
    public void ProcessItem_WhenImportContainsAtLeast1000Items_ReturnsEachQuarter()
    {
        var tracker = new ImportSyncProgressTracker(1000);
        var progressUpdates = new List<int>();

        for (var i = 0; i < 1000; i++)
        {
            var progress = tracker.ProcessItem();
            if (progress.HasValue)
            {
                progressUpdates.Add(progress.Value);
            }
        }

        Assert.Equal([25, 50, 75, 100], progressUpdates);
        Assert.Equal(1000, tracker.ProcessedItems);
    }

    [Fact]
    public void ProcessItem_WhenImportContainsFewerThan1000Items_ReturnsNoProgress()
    {
        var tracker = new ImportSyncProgressTracker(999);

        for (var i = 0; i < 999; i++)
        {
            Assert.Null(tracker.ProcessItem());
        }
    }
}
