using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Ekom.Utilities
{
    class SearchHelper
    {
        internal static string RemoveDiacritics(string text)
        {
            foreach (var characterMap in Characters)
            {
                text = text.Replace(characterMap.Key, characterMap.Value);
            }

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        internal static Dictionary<string, string> Characters = new Dictionary<string, string>
        {
            { "æ", "ae" },
            { "ð", "d" },
            { "þ", "th" },
            { "%", "" },
            { ";", "" },
            { "!", "" },
            { ",", "" },
            { "'", "" },
            { ".", "" },
            { "&", "" },
        };
        internal class SearchField
        {
            public string Name { get; set; }
            public SearchType SearchType { get; set; } = SearchType.FuzzyAndWilcard;
            public string FuzzyConfiguration { get; set; } = "0.5";
            public string Booster { get; set; }
        }
        internal enum SearchType
        {
            Exact,
            Wildcard,
            Fuzzy,
            FuzzyAndWilcard
        }
        internal static string FieldCultureName(string fieldName, string culture = null)
        {
            return string.IsNullOrEmpty(culture) ? fieldName : fieldName + "_" + culture.ToLowerInvariant();
        }
    }
}
