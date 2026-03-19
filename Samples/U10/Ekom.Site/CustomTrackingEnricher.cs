using Ekom.Klaviyo.Enrichers.TrackingEnricher;
using Ekom.Klaviyo.Models.Tracking;

namespace Ekom.Site;

public sealed class CustomTrackingEnricher : IKlaviyoTrackingEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public CustomTrackingEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public ValueTask EnrichAsync(KlaviyoTrackingEnrichmentContext context, CancellationToken ct = default)
    {
        if (context.Payload is KlaviyoAddedToCartEvent e)
        {

            if (_httpContextAccessor.HttpContext == null)
            {
                return ValueTask.CompletedTask;
            }

            var userName = _httpContextAccessor.HttpContext.User.Identity?.Name;

            if (!string.IsNullOrEmpty(userName))
            {
                if (string.IsNullOrEmpty(e.Customer.ExternalId))
                {
                    e.Customer.ExternalId = userName;
                }

                if (string.IsNullOrEmpty(e.Customer.Email) && userName.Contains("@", StringComparison.OrdinalIgnoreCase)) 
                {
                    e.Customer.Email = userName;
                }

            }
           
        }
        return ValueTask.CompletedTask;
    }
}
