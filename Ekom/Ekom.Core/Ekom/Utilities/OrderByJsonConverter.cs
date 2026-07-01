using Newtonsoft.Json;
using System.ComponentModel;
using System.Globalization;

namespace Ekom.Utilities;

public sealed class OrderByJsonConverter : JsonConverter
{
    private static readonly OrderBy Default = Configuration.Instance.DefaultProductOrderBy;
    private const bool UseDefaultOnNull = true;

    public override bool CanConvert(Type objectType)
    {
        var t = Nullable.GetUnderlyingType(objectType) ?? objectType;
        return t == typeof(OrderBy);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var isNullable = Nullable.GetUnderlyingType(objectType) != null;

        if (reader.TokenType == JsonToken.Null)
            return UseDefaultOnNull ? Default : (isNullable ? null : Default);

        if (reader.TokenType == JsonToken.String)
        {
            var s = (reader.Value as string) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(s)) return Default;

            if (Enum.TryParse<OrderBy>(s, true, out var parsed) && Enum.IsDefined(typeof(OrderBy), parsed))
                return parsed;

            return Default;
        }

        if (reader.TokenType == JsonToken.Integer)
        {
            try
            {
                var val = (OrderBy)Enum.ToObject(typeof(OrderBy), Convert.ToInt32(reader.Value));
                return Enum.IsDefined(typeof(OrderBy), val) ? val : Default;
            }
            catch { return Default; }
        }

        return Default;
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is OrderBy ob) writer.WriteValue(ob.ToString());
        else writer.WriteNull();
    }
}
public sealed class OrderByTypeConverter : EnumConverter
{
    public OrderByTypeConverter() : base(typeof(OrderBy)) { }
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        var s = value?.ToString();
        if (string.IsNullOrWhiteSpace(s)) return Configuration.Instance.DefaultProductOrderBy;

        if (Enum.TryParse<OrderBy>(s, true, out var parsed) && Enum.IsDefined(typeof(OrderBy), parsed))
            return parsed;

        return OrderBy.TitleAsc;
    }
}
