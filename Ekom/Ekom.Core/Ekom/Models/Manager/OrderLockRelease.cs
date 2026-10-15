namespace Ekom.Models.Manager;

public sealed class OrderLockReleaseRequest
{
    public string? ReleaseToken { get; set; }
}

public sealed record OrderLockReleaseResponse(string Message, string? Warning);
