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

    private RegionInfo? TryGetRegionInfo()
    {
        if (string.IsNullOrWhiteSpace(CurrencyValue))
        {
            return null;
        }

        try
        {
            return new RegionInfo(CurrencyValue);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
