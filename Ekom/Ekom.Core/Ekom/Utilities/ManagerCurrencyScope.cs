using System.Globalization;

namespace Ekom.Utilities;

internal static class ManagerCurrencyScope
{
    private static readonly IReadOnlyDictionary<string, string[]> Values = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(c => new { Culture = c.Name.ToUpperInvariant(), Code = new RegionInfo(c.Name).ISOCurrencySymbol })
        .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.Select(x => x.Culture).Append(g.Key).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);

    internal static string? Code(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim().ToUpperInvariant();
        if (Values.ContainsKey(value)) return value;
        return Values.FirstOrDefault(pair => pair.Value.Contains(value, StringComparer.OrdinalIgnoreCase)).Key;
    }

    // Only framework currency/culture identifiers enter SQL; never interpolate request values.
    internal static string Predicate(string? currency, string column = "Currency")
    {
        if (string.IsNullOrWhiteSpace(currency)) return string.Empty;
        string? code = Code(currency);
        return code == null ? " AND 1 = 0" : $" AND UPPER(LTRIM(RTRIM({column}))) IN ({string.Join(",", Values[code].Select(value => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'"))})";
    }
}
