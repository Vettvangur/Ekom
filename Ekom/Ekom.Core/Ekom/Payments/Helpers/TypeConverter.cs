using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.Helpers;

/// <summary>
/// https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Configuration.Binder/src/ConfigurationBinder.cs
/// </summary>
internal class PaymentsTypeConverter
{
    private static bool TryConvertValue(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type type,
        string value, out object? result, out Exception? error)
    {
        error = null;
        result = null;
        if (type == typeof(object))
        {
            result = value;
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }
            return TryConvertValue(Nullable.GetUnderlyingType(type)!, value, out result, out error);
        }

        TypeConverter converter = TypeDescriptor.GetConverter(type);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                result = converter.ConvertFromInvariantString(value);
            }
            catch (Exception ex)
            {
                error = new InvalidOperationException("Unable to convert value: " + value + " to type: " + type, ex);
            }
            return true;
        }

        if (type == typeof(byte[]))
        {
            try
            {
                result = Convert.FromBase64String(value);
            }
            catch (FormatException ex)
            {
                error = new InvalidOperationException("Unable to convert base64 string to byte array", ex);
            }
            return true;
        }

        return false;
    }

    public static object? ConvertValue(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        Type type,
        string value)
    {
        TryConvertValue(type, value, out object? result, out Exception? error);
        if (error != null)
        {
            throw error;
        }
        return result;
    }
}
