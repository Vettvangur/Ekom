using System.Globalization;

namespace Ekom.Models.Comparers
{
    internal class SemiNumericComparer : IComparer<string>
    {
        public static bool IsDecimal(string value)
        {
            return decimal.TryParse(value, out _);
        }

        public static string ReplaceComma(string value)
        {
            return value.Replace(".", ",", StringComparison.InvariantCulture);
        }
        
        /// <inheritdoc />
        public int Compare(string s1, string s2)
        {
            const int S1GreaterThanS2 = 1;
            const int S2GreaterThanS1 = -1;

            var IsDecimal1 = IsDecimal(ReplaceComma(s1));
            var IsDecimal2 = IsDecimal(ReplaceComma(s2));

            if (IsDecimal1 && IsDecimal2)
            {
                var i1 = Convert.ToDecimal(ReplaceComma(s1));
                var i2 = Convert.ToDecimal(ReplaceComma(s2));

                if (i1 > i2)
                {
                    return S1GreaterThanS2;
                }

                if (i1 < i2)
                {
                    return S2GreaterThanS1;
                }

                return 0;
            }

            if (IsDecimal1)
            {
                return S2GreaterThanS1;
            }

            if (IsDecimal2)
            {
                return S1GreaterThanS2;
            }

            return string.Compare(s1, s2, true, CultureInfo.InvariantCulture);
        }
    }
}
