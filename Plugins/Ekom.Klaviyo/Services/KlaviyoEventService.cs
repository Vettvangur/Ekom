namespace Ekom.Klaviyo.API;

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

