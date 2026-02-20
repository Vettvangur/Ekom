using System.Text;

namespace Ekom.Klaviyo.Helpers;

public static class KlaviyoPhoneNormalizer
{
    public static string? NormalizePhone(string? input, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (string.IsNullOrWhiteSpace(countryCode)) return null;

        var normalizedCountryCode = countryCode.Trim().TrimStart('+');
        if (string.IsNullOrWhiteSpace(normalizedCountryCode)) return null;

        var trimmed = input.Trim();
        var builder = new StringBuilder(trimmed.Length);

        foreach (var ch in trimmed)
        {
            if (char.IsDigit(ch))
                builder.Append(ch);
            else if (ch == '+' && builder.Length == 0)
                builder.Append(ch);
        }

        var normalized = builder.ToString();
        if (string.IsNullOrWhiteSpace(normalized)) return null;

        if (normalized.StartsWith("00", StringComparison.Ordinal))
            normalized = $"+{normalized[2..]}";

        if (normalized.StartsWith("+", StringComparison.Ordinal))
        {
            return normalized.StartsWith($"+{normalizedCountryCode}", StringComparison.Ordinal)
                ? normalized
                : null;
        }

        if (normalized.StartsWith(normalizedCountryCode, StringComparison.Ordinal))
            return $"+{normalized}";

        return $"+{normalizedCountryCode}{normalized}";
    }
}
