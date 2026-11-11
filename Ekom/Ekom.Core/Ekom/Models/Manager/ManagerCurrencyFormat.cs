using Ekom.Utilities;
using System.Globalization;

namespace Ekom.Models.Manager;

/// <summary>
/// How a store currency is written, taken from the culture configured on the store,
/// so the manager can format amounts without relying on the browser's locale data.
/// </summary>
public sealed class ManagerCurrencyFormat
{
    public string Code { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string GroupSeparator { get; set; } = string.Empty;
    public string DecimalSeparator { get; set; } = string.Empty;
    public int DecimalDigits { get; set; }

    /// <summary>
    /// <see cref="NumberFormatInfo.CurrencyPositivePattern"/>: 0 = $n, 1 = n$, 2 = $ n, 3 = n $.
    /// </summary>
    public int PositivePattern { get; set; }

    /// <param name="code">ISO currency code.</param>
    /// <param name="culture">Culture configured on the store for this currency, when there is one.</param>
    public static ManagerCurrencyFormat? From(string code, string? culture)
    {
        NumberFormatInfo? format = TryGetCulture(culture, code)?.NumberFormat ?? CultureHelper.GetCultureInfo(code)?.NumberFormat;
        if (format == null) return null;

        return new ManagerCurrencyFormat
        {
            Code = code,
            Symbol = format.CurrencySymbol,
            GroupSeparator = format.CurrencyGroupSeparator,
            DecimalSeparator = format.CurrencyDecimalSeparator,
            DecimalDigits = format.CurrencyDecimalDigits,
            PositivePattern = format.CurrencyPositivePattern,
        };
    }

    private static CultureInfo? TryGetCulture(string? culture, string code)
    {
        // A bare currency code is not a culture name.
        if (string.IsNullOrWhiteSpace(culture) || !culture.Contains('-', StringComparison.Ordinal)) return null;
        try
        {
            // Unknown names can still yield a culture with placeholder formatting,
            // so only trust one whose region really uses this currency.
            var info = CultureInfo.GetCultureInfo(culture);
            return string.Equals(new RegionInfo(info.Name).ISOCurrencySymbol, code, StringComparison.OrdinalIgnoreCase) ? info : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
