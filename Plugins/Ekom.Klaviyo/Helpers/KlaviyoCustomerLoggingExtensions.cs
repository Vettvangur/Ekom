using Ekom.Klaviyo.Models.Subscriptions;

namespace Ekom.Klaviyo.Helpers;

public static class KlaviyoCustomerLoggingExtensions
{
    public static string IdentifierForLogs(this KlaviyoCustomer c)
    {
        if (c is null) return "(null)";

        if (!string.IsNullOrWhiteSpace(c.ExternalId))
            return $"ext:{c.ExternalId}";

        if (!string.IsNullOrWhiteSpace(c.KlaviyoProfileId))
            return $"kid:{c.KlaviyoProfileId}";

        if (!string.IsNullOrWhiteSpace(c.Email))
            return MaskEmail(c.Email);

        if (!string.IsNullOrWhiteSpace(c.PhoneNumber))
            return MaskPhone(c.PhoneNumber);

        return "(unknown)";
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        if (at <= 2) return "***" + email[at..];
        return email[..2] + "***" + email[at..];
    }

    private static string MaskPhone(string phone)
    {
        phone = phone.Trim();
        if (phone.Length < 4) return "***";
        return "***" + phone[^4..];
    }
}
