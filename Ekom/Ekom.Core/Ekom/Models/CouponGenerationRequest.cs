namespace Ekom.Models;

public class CouponGenerationRequest
{
    public int Count { get; set; }
    public int NumberAvailable { get; set; } = 1;
    public string Prefix { get; set; } = string.Empty;
    public int RandomLength { get; set; } = 8;
    public CouponGenerationCharacterSet CharacterSet { get; set; } = CouponGenerationCharacterSet.UppercaseAlphanumeric;
}

public enum CouponGenerationCharacterSet
{
    UppercaseAlphanumeric,
    Numbers,
    Letters,
}
