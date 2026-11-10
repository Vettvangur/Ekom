namespace Ekom.Models.Manager;

public sealed class RaiseOrderReviewRequest
{
    public string Source { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ResolveOrderReviewRequest
{
    public bool Resolved { get; set; } = true;
    public string? Comment { get; set; }
}
