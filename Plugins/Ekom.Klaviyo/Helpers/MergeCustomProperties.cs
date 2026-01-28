namespace Ekom.Klaviyo.Helpers;

using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static class CustomPropertiesMerger
{
    private static readonly Regex _snakeCleanup =
        new Regex(@"_+", RegexOptions.Compiled);

    internal static void MergeCustomProperties(
        JsonObject target,
        IDictionary<string, object?> source)
    {
        foreach (var (key, value) in source)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            // Treat empty/whitespace strings as null -> don't emit
            if (value is string s && string.IsNullOrWhiteSpace(s))
                continue;

            // If it's explicitly null -> don't emit
            if (value is null)
                continue;

            var normalizedKey = ToSnakeCase(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
                continue;

            target[normalizedKey] = JsonValue.Create(value);
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var sb = new StringBuilder(input.Length + 10);

        UnicodeCategory? previousCategory = null;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (c == '_')
            {
                sb.Append('_');
                previousCategory = null;
                continue;
            }

            var currentCategory = char.GetUnicodeCategory(c);

            if (currentCategory == UnicodeCategory.UppercaseLetter)
            {
                if (i > 0 &&
                    previousCategory != UnicodeCategory.UppercaseLetter &&
                    previousCategory != UnicodeCategory.DecimalDigitNumber)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else if (currentCategory == UnicodeCategory.DecimalDigitNumber)
            {
                if (i > 0 &&
                    previousCategory != UnicodeCategory.DecimalDigitNumber)
                {
                    sb.Append('_');
                }

                sb.Append(c);
            }
            else
            {
                sb.Append(char.ToLowerInvariant(c));
            }

            previousCategory = currentCategory;
        }

        // Normalize multiple underscores and trim
        var result = _snakeCleanup.Replace(sb.ToString(), "_")
                                  .Trim('_');

        return result;
    }
}
