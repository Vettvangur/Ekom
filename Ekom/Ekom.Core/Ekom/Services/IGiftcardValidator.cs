using Ekom.Models;

namespace Ekom.Services;

/// <summary>
/// Resolves a gift-card code submitted through the public HTTP API into a trusted
/// <see cref="Giftcard"/>. The application owns this integration: Ekom never trusts an
/// amount, validity date or claim metadata supplied by the browser.
/// </summary>
/// <remarks>
/// Register an implementation (for example
/// <c>services.AddScoped&lt;IGiftcardValidator, MyGiftcardValidator&gt;()</c>) to enable
/// <c>POST /ekom/order/giftcards</c>. Without one the route rejects every request.
/// Return <c>null</c> when the code is unknown, expired or has no balance. The returned
/// amount should not exceed the balance the gift-card system can actually honour.
/// </remarks>
public interface IGiftcardValidator
{
    /// <summary>
    /// Look up <paramref name="code"/> in the gift-card system.
    /// </summary>
    /// <param name="code">Trimmed code as submitted by the customer.</param>
    /// <param name="order">The customer's current basket.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The gift card to attach to the order, or <c>null</c> to reject the code.</returns>
    Task<Giftcard?> ValidateAsync(string code, IOrderInfo order, CancellationToken ct = default);
}
