namespace Ekom.Models;

public sealed class Giftcard
{
    public decimal Amount { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime? UsedDate { get; set; }
    public string? ClaimId { get; set; }
    public string? TransactionId { get; set; }
    public DateTime? ClaimDate { get; set; }
    public bool Claimed { get; set; }
    public DateTime? ValidUntil { get; set; }
}
