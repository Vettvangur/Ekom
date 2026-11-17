using System.Collections.Concurrent;
using System.Globalization;

namespace Ekom.Models;

public class CurrencyModel
{
    public string CurrencyFormat { get; set; }
    public string CurrencyValue { get; set; }
    public string CurrencySymbol
    {
        get
        {
            return TryGetRegionInfo()?.CurrencySymbol ?? string.Empty;
        }
    }
    public string ISOCurrencySymbol
    {
        get
        {
            return TryGetRegionInfo()?.ISOCurrencySymbol ?? string.Empty;
        }
    }
    public int CurrencyDecimalDigits
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CurrencyValue))
            {
                return 2;
            }

            try
            {
                return CultureInfo.GetCultureInfo(CurrencyValue).NumberFormat.CurrencyDecimalDigits;
            }
            catch (CultureNotFoundException)
            {
                return 2;
            }
        }
    }

    // Constructing a RegionInfo loads culture data, far too slow to repeat for every price calculation.
    private static readonly ConcurrentDictionary<string, RegionInfo?> _regions = new(StringComparer.Ordinal);

    private RegionInfo? TryGetRegionInfo()
    {
        if (string.IsNullOrWhiteSpace(CurrencyValue))
        {
            return null;
        }

        return _regions.GetOrAdd(CurrencyValue, static value =>
        {
            try
            {
                return new RegionInfo(value);
            }
            catch (ArgumentException)
            {
                return null;
            }
        });
    }
}
