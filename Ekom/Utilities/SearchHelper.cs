using System.Globalization;
using System.Text;

namespace Ekom.Utilities
{
    internal static class SearchHelper
    {
        internal static string RemoveDiacritics(string text)
        {
            foreach (KeyValuePair<string, string> characterMap in Characters)
            {
                text = text.Replace(characterMap.Key, characterMap.Value);
            }

            string normalizedString = text.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
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

        public static string FieldCultureName(this string fieldName, string culture = null)
        {
            return string.IsNullOrEmpty(culture) ? fieldName : fieldName + "_" + culture.ToLowerInvariant();
        }
    }
}
