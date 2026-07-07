using Newtonsoft.Json.Linq;

namespace Ekom.Utilities;

public static class JsonHelper
{
    public static JToken ToCamelCaseKeys(JToken token)
    {
        if (token is JObject obj)
        {
            var newObj = new JObject();
            foreach (var property in obj.Properties())
            {
                var camelKey = ToCamelCase(property.Name);
                newObj[camelKey] = ToCamelCaseKeys(property.Value);
            }
            return newObj;
        }

        if (token is JArray array)
        {
            var newArray = new JArray();
            foreach (var item in array)
            {
                newArray.Add(ToCamelCaseKeys(item));
            }
            return newArray;
        }

        return token;
    }

    private static string ToCamelCase(string key)
    {
        if (string.IsNullOrEmpty(key) || !char.IsUpper(key[0]))
            return key;

        return char.ToLowerInvariant(key[0]) + key.Substring(1);
    }
    
}
