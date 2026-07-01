namespace Ekom.Services;

internal class UtilityService
{
    public static DateTime ConvertToDatetime(string value)
    {
        try
        {
            return DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
        }
        catch
        {
            return new DateTime(Convert.ToInt64(value));
        }
    }

    public static bool ConvertUdiToGuid(string udi, out Guid guid)
    {
        guid = Guid.Empty;

        if (string.IsNullOrEmpty(udi))
        {
            return false;
        }

        if (!udi.StartsWith("umb://"))
        {
            return false;
        }

        string value = udi.Substring(udi.LastIndexOf('/') + 1);

        if (Guid.TryParse(value, out guid))
        {
            return true;
        }

        return false;
    }

    public static bool ConvertUdisToGuids(string udis, out IEnumerable<Guid> guids)
    {
        guids = Enumerable.Empty<Guid>();

        if (string.IsNullOrEmpty(udis))
        {
            return false;
        }

        if (!udis.StartsWith("umb://"))
        {
            return false;
        }

        List<Guid> list = new List<Guid>();

        foreach (string udi in udis.Split(','))
        {
            if (ConvertUdiToGuid(udi, out Guid guid))
            {
                list.Add(guid);
            }
            else
            {
                return false;
            }
        }

        guids = list;

        return true;
    }
}
