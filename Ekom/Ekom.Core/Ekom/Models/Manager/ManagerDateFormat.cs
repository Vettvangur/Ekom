using System.Globalization;

namespace Ekom.Models.Manager;

/// <summary>
/// How the store's culture writes dates, so the manager can format them without relying on
/// the browser's locale data, which lacks some cultures. Patterns are .NET custom format strings.
/// </summary>
public sealed class ManagerDateFormat
{
    public string Culture { get; set; } = string.Empty;
    public string ShortDate { get; set; } = string.Empty;
    public string ShortTime { get; set; } = string.Empty;
    public string MonthDay { get; set; } = string.Empty;
    public string YearMonth { get; set; } = string.Empty;
    public string DateSeparator { get; set; } = string.Empty;
    public string TimeSeparator { get; set; } = string.Empty;
    public string AmDesignator { get; set; } = string.Empty;
    public string PmDesignator { get; set; } = string.Empty;

    /// <summary>January first.</summary>
    public IReadOnlyList<string> MonthNames { get; set; } = Array.Empty<string>();

    /// <summary>January first.</summary>
    public IReadOnlyList<string> AbbreviatedMonthNames { get; set; } = Array.Empty<string>();

    /// <summary>Sunday first.</summary>
    public IReadOnlyList<string> ShortestDayNames { get; set; } = Array.Empty<string>();

    /// <param name="culture">Name of the culture configured on the store.</param>
    public static ManagerDateFormat? From(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return null;

        CultureInfo info;
        try
        {
            info = CultureInfo.GetCultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }

        DateTimeFormatInfo format = info.DateTimeFormat;
        return new ManagerDateFormat
        {
            Culture = info.Name,
            ShortDate = format.ShortDatePattern,
            ShortTime = format.ShortTimePattern,
            MonthDay = format.MonthDayPattern,
            YearMonth = format.YearMonthPattern,
            DateSeparator = format.DateSeparator,
            TimeSeparator = format.TimeSeparator,
            AmDesignator = format.AMDesignator,
            PmDesignator = format.PMDesignator,
            // The calendar arrays end with an empty thirteenth month.
            MonthNames = format.MonthNames.Take(12).ToArray(),
            AbbreviatedMonthNames = format.AbbreviatedMonthNames.Take(12).ToArray(),
            ShortestDayNames = format.ShortestDayNames,
        };
    }
}
