using System.Text;

namespace uWebshop.Utilities
{
    public static class StringExtension
    {
        private static string RemoveAccent(this string txt)
        {
            byte[] bytes = Encoding.GetEncoding("Cyrillic").GetBytes(txt);
            return Encoding.ASCII.GetString(bytes);
        }

        /// <summary>
        /// Ensure string ends in one and only one '/'
        /// </summary>
        /// <param name="value">String to examine</param>
        /// <returns>String ending in one and only one '/'</returns>
        public static string AddTrailing(this string value)
        {
            if (value.Length == 0)
            {
                return "/";
            }
            else if (value[value.Length - 1] != '/')
            {
                value += "/";
            }

            return value;
        }

        public static bool ConvertToBool(this string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                value = value.ToLowerInvariant();

                if (value == "1" || value == "true" || value == "y")
                {
                    return true;
                }
            }

            return false;

        }
    }
}
