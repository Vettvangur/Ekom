namespace Ekom.Klaviyo.Services;

public interface IKlaviyoEventService
{
    ValueTask TrackAsync(string name, object payload, CancellationToken ct = default);
}

public sealed class KlaviyoEventService : IKlaviyoEventService
{
    public ValueTask TrackAsync(string name, object payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}


// Placed Order
// Started Checkout
// Added to Cart
// Viewed Product
