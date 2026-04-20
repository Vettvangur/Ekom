namespace Ekom.Models.Manager;

public sealed class OrderManagerAction
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string Look { get; init; } = "outline";

    public bool Enabled { get; init; } = true;

    public string? ConfirmMessage { get; init; }

    public int SortOrder { get; init; }
}
