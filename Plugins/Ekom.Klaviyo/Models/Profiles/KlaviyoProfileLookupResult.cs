using Ekom.Klaviyo.Models.Profiles;
using System;
using System.Linq;

namespace Ekom.Klaviyo.Models.Profiles;

public sealed class KlaviyoProfileLookupResult
{
    public string? ProfileId { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? ExternalId { get; init; }

    public IReadOnlyList<KlaviyoProfileConsentChannel> SubscribedChannels { get; set; }
        = Array.Empty<KlaviyoProfileConsentChannel>();

    public bool IsEmailSubscribed
        => SubscribedChannels.Contains(KlaviyoProfileConsentChannel.Email);
}
