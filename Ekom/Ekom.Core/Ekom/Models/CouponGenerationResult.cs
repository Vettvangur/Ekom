namespace Ekom.Models;

public class CouponGenerationResult
{
    public int Created { get; set; }
    public int SkippedDuplicates { get; set; }
    public IReadOnlyList<string> Coupons { get; set; } = Array.Empty<string>();
}
