using Ekom.Brevo.Models;
using Ekom.Models;

namespace Ekom.Brevo.Services;

public interface IBrevoContactsService
{
    ValueTask<BrevoSubmission> UpsertAsync(BrevoContact contact, CancellationToken cancellationToken = default);
    /// <summary>Call only after explicit email marketing consent. Null listId uses store/global configuration.</summary>
    ValueTask<BrevoSubmission> SubscribeAsync(BrevoContact contact, long? listId = null, CancellationToken cancellationToken = default);
    /// <summary>Blocks email marketing across the Brevo account, not transactional email.</summary>
    ValueTask<BrevoSubmission> UnsubscribeAsync(string storeAlias, string email, CancellationToken cancellationToken = default);
    /// <summary>Removes list membership without changing account-wide marketing consent.</summary>
    ValueTask<BrevoSubmission> RemoveFromListAsync(string storeAlias, string email, long listId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BrevoList>> GetListsAsync(string storeAlias, CancellationToken cancellationToken = default);
}

public interface IBrevoOrderService
{
    ValueTask<BrevoSubmission> TrackOrderAsync(IOrderInfo order, CancellationToken cancellationToken = default);
}

public interface IBrevoTransactionalService
{
    /// <summary>Commits a serialized snapshot to the outbox. Completion observes the API result in this process; use IBrevoDeliveryService after restart.</summary>
    ValueTask<BrevoSubmission> SendAsync(BrevoTransactionalMessage message, CancellationToken cancellationToken = default);
}
