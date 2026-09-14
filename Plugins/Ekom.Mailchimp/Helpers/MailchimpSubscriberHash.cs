using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Mailchimp.Helpers;

internal static class MailchimpSubscriberHash
{
    [SuppressMessage("Security", "CA5351", Justification = "Mailchimp requires an MD5 subscriber hash as a non-security identifier.")]
    [SuppressMessage("Globalization", "CA1308", Justification = "Mailchimp explicitly requires a lowercase email address and lowercase hexadecimal MD5 hash.")]
    internal static string Create(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        byte[] value = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        return Convert.ToHexString(MD5.HashData(value)).ToLowerInvariant();
    }
}
