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

        public static string AddTrailing(this string value)
        {
            try
            {
                if (value.Substring(value.Length - 1, 1) != "/")
                {
                    value = value + "/";

                    return value;
                }
                else
                {
                    return value;
                }
            }
            catch {
                return value;
            }
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
