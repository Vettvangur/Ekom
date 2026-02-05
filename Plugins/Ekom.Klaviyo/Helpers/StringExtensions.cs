namespace Ekom.Klaviyo.Helpers;

internal static class StringExtensions
{
    public static bool IsBoolean(this string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("1", StringComparison.OrdinalIgnoreCase) || value.Equals("on", StringComparison.OrdinalIgnoreCase);

    }
}
