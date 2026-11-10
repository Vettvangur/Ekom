using System.Diagnostics.CodeAnalysis;
using Ekom.Models;

namespace Ekom.Services;

/// <summary>Durable order reviews. Trusted integrations may raise issues without an HTTP request.</summary>
public interface IOrderReviewService
{
    /// <summary>Returns the matching open issue, or creates one. Resolved issues are never reopened.</summary>
    [SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "Raises a persisted review issue, not a CLR event.")]
    Task<OrderReview> RaiseAsync(Guid orderId, string source, string code, string title, string message, CancellationToken ct = default);

    /// <summary>Lists open reviews by default, or paginated history including resolved reviews.</summary>
    Task<IReadOnlyList<OrderReview>> GetReviewsAsync(Guid orderId, bool includeResolved = false, int skip = 0, int take = 50, CancellationToken ct = default);

    /// <summary>Resolves once; subsequent calls preserve the original reviewer and comment. Null means not found.</summary>
    Task<OrderReview?> ResolveAsync(Guid orderId, Guid reviewId, string resolvedBy, string? comment = null, CancellationToken ct = default);
}
