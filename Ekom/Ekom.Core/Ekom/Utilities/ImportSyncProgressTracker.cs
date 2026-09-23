namespace Ekom.Utilities;

internal sealed class ImportSyncProgressTracker
{
    private const int MinimumItemCount = 1000;
    private const int ProgressIncrement = 25;

    private int _nextProgress = ProgressIncrement;

    public ImportSyncProgressTracker(int totalItems)
    {
        TotalItems = Math.Max(totalItems, 0);
    }

    public int TotalItems { get; }

    public int ProcessedItems { get; private set; }

    public int? ProcessItem()
    {
        if (TotalItems < MinimumItemCount)
        {
            return null;
        }

        ProcessedItems++;

        if (ProcessedItems * 100 < TotalItems * _nextProgress)
        {
            return null;
        }

        var progress = _nextProgress;
        _nextProgress += ProgressIncrement;
        return progress;
    }
}
