﻿using log4net;
using Newtonsoft.Json;
using Our.Umbraco.Vorto.Models;
using System.Reflection;
using System.Text;

namespace Ekom.Utilities
{
    static class StringExtension
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

        /// <summary>
        /// Coerces a string to a boolean value, case insensitive and also registers true for 1 and y
        /// </summary>
        /// <param name="value">String value to convert to bool</param>
        public static bool ConvertToBool(this string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                var bVal = bool.TryParse(value, out bool result);

                if (bVal)
                {
                    return result;
                }
                else
                {
                    return (value == "1" || value.ToLower() == "y");
                }
            }

            return false;
        }

        public static bool IsVortoValue(this string value)
        {

            try
            {
                if (value.IsJson())
                {
                    var o = JsonConvert.DeserializeObject<VortoValue>(value);

                    return true;
                }

            } catch {}

            return false;

        }

        // Maybe this should return T and not force String
        public static string GetVortoValue(this string value, string storeAlias)
        {

            try
            {

                if (!string.IsNullOrEmpty(value))
                {

                    if (value.IsJson())
                    {
                        var o = JsonConvert.DeserializeObject<VortoValue>(value);

                        if (o != null)
                        {

                            object itemValue = null;

                            var foundValue = o.Values.TryGetValue(storeAlias, out itemValue);

                            if (foundValue)
                            {

                                if (itemValue != null)
                                {
                                    return itemValue.ToString();
                                }
                            }


                        }
                    } else
                    {
                        return value;
                    }

                }

            } catch {}

            return string.Empty;

        }

        public static bool IsJson(this string input)
        {
            input = input.Trim();
            return input.StartsWith("{") && input.EndsWith("}")
                   || input.StartsWith("[") && input.EndsWith("]");
        }
    }
}