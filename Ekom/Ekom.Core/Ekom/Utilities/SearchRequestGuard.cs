using Ekom.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ekom.Utilities;

/// <summary>
/// Keeps caller-supplied search options from changing the meaning of the Lucene query
/// Ekom builds, and limits what an anonymous HTTP caller may ask for.
/// </summary>
public static class SearchRequestGuard
{
    /// <summary>Longest search text accepted from a public request.</summary>
    public const int MaxPublicSearchQueryLength = 200;

    private const string DefaultFuzzyConfiguration = "0.5";

    private static readonly Regex FieldNameRegex = new("^[A-Za-z0-9_][A-Za-z0-9_-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// True when <paramref name="name"/> is a plain index field name with no Lucene syntax.
    /// </summary>
    public static bool IsValidFieldName(string? name)
        => !string.IsNullOrEmpty(name) && name.Length <= 100 && FieldNameRegex.IsMatch(name);

    /// <summary>
    /// Returns the boost suffix in canonical form (for example "^4.0"), or an empty string
    /// when the supplied value is not a "^number" boost.
    /// </summary>
    public static string NormalizeBooster(string? booster)
    {
        if (string.IsNullOrWhiteSpace(booster))
        {
            return string.Empty;
        }

        var value = booster.Trim();
        if (value.Length < 2 || value[0] != '^' || !TryParsePlainNumber(value[1..], out var number))
        {
            return string.Empty;
        }

        return "^" + number.ToString("0.0###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the fuzzy similarity in canonical form, falling back to the default
    /// when the supplied value is not a number between 0 and 1.
    /// </summary>
    public static string NormalizeFuzzyConfiguration(string? fuzzyConfiguration)
    {
        if (string.IsNullOrWhiteSpace(fuzzyConfiguration)
            || !TryParsePlainNumber(fuzzyConfiguration.Trim(), out var number)
            || number > 1m)
        {
            return DefaultFuzzyConfiguration;
        }

        return number.ToString("0.0###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Copies <paramref name="fields"/>, dropping entries whose name is not a plain field name
    /// and normalizing boost and fuzzy values. Safe to interpolate into a Lucene query.
    /// </summary>
    public static List<EkomSearchField> SanitizeFields(IEnumerable<EkomSearchField>? fields)
    {
        var result = new List<EkomSearchField>();
        if (fields == null)
        {
            return result;
        }

        foreach (var field in fields)
        {
            if (field == null || !IsValidFieldName(field.Name))
            {
                continue;
            }

            result.Add(new EkomSearchField
            {
                Name = field.Name,
                SearchType = Enum.IsDefined(field.SearchType) ? field.SearchType : EkomSearchType.Exact,
                Booster = NormalizeBooster(field.Booster),
                FuzzyConfiguration = NormalizeFuzzyConfiguration(field.FuzzyConfiguration),
            });
        }

        return result;
    }

    /// <summary>
    /// Applies the restrictions for a query that arrived over the public HTTP API:
    /// the Examine index cannot be chosen, only allow-listed fields may be searched,
    /// and the search text is capped in length.
    /// </summary>
    public static void RestrictToPublic(ProductQueryBase? query, IReadOnlyCollection<string> allowedFields)
    {
        if (query == null)
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(allowedFields);

        if (query is SearchRequest searchRequest)
        {
            searchRequest.ExamineIndex = string.Empty;
        }

        if (query.SearchQuery?.Length > MaxPublicSearchQueryLength)
        {
            query.SearchQuery = query.SearchQuery[..MaxPublicSearchQueryLength];
        }

        if (query.SearchFields?.Count > 0)
        {
            var allowed = new HashSet<string>(allowedFields, StringComparer.OrdinalIgnoreCase);

            // An empty list makes the search service fall back to its own default fields.
            query.SearchFields = SanitizeFields(query.SearchFields)
                .Where(field => allowed.Contains(field.Name))
                .ToList();
        }
    }

    private static bool TryParsePlainNumber(string value, out decimal number)
    {
        number = 0;

        // Digits and a single dot only: no sign, exponent or whitespace.
        if (value.Length == 0 || value.Length > 10 || value.Any(c => !char.IsAsciiDigit(c) && c != '.'))
        {
            return false;
        }

        return decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);
    }
}
