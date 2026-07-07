using System.Globalization;
using System.Text;

namespace Ekom.Umb.Services;

internal static class ExamineSearchTextNormalizer
{
    public const string NormalizedFieldSuffix = "_normalized";

    public static string NormalizedFieldName(string fieldName)
        => fieldName.EndsWith(NormalizedFieldSuffix, StringComparison.OrdinalIgnoreCase)
            ? fieldName
            : fieldName + NormalizedFieldSuffix;

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSeparator = true;

        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            AppendNormalizedCharacter(char.ToLowerInvariant(c), builder, ref previousWasSeparator);
        }

        return builder.ToString().Trim();
    }

    public static IReadOnlyList<string> Tokenize(string value, bool normalize)
    {
        var searchValue = normalize ? Normalize(value) : NormalizeSeparators(value);
        if (string.IsNullOrWhiteSpace(searchValue))
            return [];

        return searchValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeSeparators(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = true;

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                previousWasSeparator = false;
                continue;
            }

            AppendSeparator(builder, ref previousWasSeparator);
        }

        return builder.ToString().Trim();
    }

    private static void AppendNormalizedCharacter(char c, StringBuilder builder, ref bool previousWasSeparator)
    {
        switch (c)
        {
            case 'æ':
                builder.Append("ae");
                previousWasSeparator = false;
                return;
            case 'ð':
                builder.Append('d');
                previousWasSeparator = false;
                return;
            case 'þ':
                builder.Append("th");
                previousWasSeparator = false;
                return;
        }

        if (char.IsLetterOrDigit(c))
        {
            builder.Append(c);
            previousWasSeparator = false;
            return;
        }

        AppendSeparator(builder, ref previousWasSeparator);
    }

    private static void AppendSeparator(StringBuilder builder, ref bool previousWasSeparator)
    {
        if (previousWasSeparator)
            return;

        builder.Append(' ');
        previousWasSeparator = true;
    }
}
