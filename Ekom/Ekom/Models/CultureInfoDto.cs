using System.Globalization;

namespace Ekom.Models;

public sealed class CultureInfoDto
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string EnglishName { get; init; } = string.Empty;
    public string NativeName { get; init; } = string.Empty;
    public string TwoLetterISOLanguageName { get; init; } = string.Empty;
    public string ThreeLetterISOLanguageName { get; init; } = string.Empty;
    public string IetfLanguageTag { get; init; } = string.Empty;
    public bool IsNeutralCulture { get; init; }
    public int LCID { get; init; }
    public string Parent { get; init; } = string.Empty;

    public static CultureInfoDto From(CultureInfo culture)
    {
        return new CultureInfoDto
        {
            Name = culture.Name,
            DisplayName = culture.DisplayName,
            EnglishName = culture.EnglishName,
            NativeName = culture.NativeName,
            TwoLetterISOLanguageName = culture.TwoLetterISOLanguageName,
            ThreeLetterISOLanguageName = culture.ThreeLetterISOLanguageName,
            IetfLanguageTag = culture.IetfLanguageTag,
            IsNeutralCulture = culture.IsNeutralCulture,
            LCID = culture.LCID,
            Parent = culture.Parent?.Name ?? string.Empty,
        };
    }
}
