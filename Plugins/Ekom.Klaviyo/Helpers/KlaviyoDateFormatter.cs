using System.Globalization;

namespace Ekom.Klaviyo.Helpers;

internal static class KlaviyoDateFormatter
{
    private const string DateTimeFormat = "yyyy-MM-dd'T'HH':'mm':'ss'Z'";

    internal static string ToKlaviyoDateTime(this DateTimeOffset value)
        => value.ToUniversalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    internal static string? ToKlaviyoDateTime(this DateTimeOffset? value)
        => value?.ToKlaviyoDateTime();
}
