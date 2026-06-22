using System.Globalization;

namespace Ekom.Utilities;

public static class VatParser
{
    private const NumberStyles VatNumberStyles = NumberStyles.AllowDecimalPoint
                                                | NumberStyles.AllowLeadingSign
                                                | NumberStyles.AllowLeadingWhite
                                                | NumberStyles.AllowTrailingWhite;

    public static bool TryParsePercentageRate(string? value, out decimal vat)
    {
        vat = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (decimal.TryParse(value, VatNumberStyles, CultureInfo.InvariantCulture, out var percentage))
        {
            vat = percentage / 100;
            return true;
        }

        if (value.Contains('.') || value.Count(x => x == ',') != 1)
        {
            return false;
        }

        var normalizedValue = value.Replace(',', '.');
        if (!decimal.TryParse(normalizedValue, VatNumberStyles, CultureInfo.InvariantCulture, out percentage))
        {
            return false;
        }

        vat = percentage / 100;
        return true;
    }
}
