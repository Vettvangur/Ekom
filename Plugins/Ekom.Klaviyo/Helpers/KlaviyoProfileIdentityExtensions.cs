using Ekom.Klaviyo.Models.Profiles;

namespace Ekom.Klaviyo.Helpers;

public static class KlaviyoProfileIdentityExtensions
{
    public static KlaviyoProfileIdentity ToIdentity(this KlaviyoCustomer customer)
        => new(
            Email: customer.Email,
            PhoneNumber: customer.PhoneNumber,
            ExternalId: customer.ExternalId);
}
