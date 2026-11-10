using System.Diagnostics.CodeAnalysis;
using Ekom.Models;
using LinqToDB;
using System.Data;

namespace Ekom.Services;

internal sealed class OrderReviewService : IOrderReviewService
{
    private readonly DatabaseFactory _databaseFactory;

    public OrderReviewService(DatabaseFactory databaseFactory) => _databaseFactory = databaseFactory;

    [SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "Raises a persisted review issue, not a CLR event.")]
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Integration source and code identifiers use canonical lowercase.")]
    public async Task<OrderReview> RaiseAsync(Guid orderId, string source, string code, string title, string message, CancellationToken ct = default)
    {
        ValidateOrderId(orderId);
        source = Required(source, 100, nameof(source)).ToLowerInvariant();
        code = Required(code, 100, nameof(code)).ToLowerInvariant();
        title = Required(title, 200, nameof(title));
        message = Required(message, 4000, nameof(message));

        await using var db = _databaseFactory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        if (!await db.OrderData.AnyAsync(x => x.UniqueId == orderId, ct).ConfigureAwait(false))
            throw new KeyNotFoundException("Order not found.");

        var existing = await db.OrderReviews.FirstOrDefaultAsync(x => x.OrderId == orderId &&
            x.Source == source && x.Code == code && x.ResolvedUtc == null, ct).ConfigureAwait(false);
        if (existing != null)
        {
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return existing;
        }

        var review = new OrderReview
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Source = source,
            Code = code,
            Title = title,
            Message = message,
            CreatedUtc = DateTime.UtcNow,
        };
        await db.InsertAsync(review, token: ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return review;
    }

    public async Task<IReadOnlyList<OrderReview>> GetReviewsAsync(Guid orderId, bool includeResolved = false, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        ValidateOrderId(orderId);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        if (take < 1 || take > 200) throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 200.");

        await using var db = _databaseFactory.GetDatabase();
        return await db.OrderReviews.Where(x => x.OrderId == orderId && (includeResolved || x.ResolvedUtc == null))
            .OrderByDescending(x => x.CreatedUtc).ThenBy(x => x.Id)
            .Skip(skip).Take(take).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<OrderReview?> ResolveAsync(Guid orderId, Guid reviewId, string resolvedBy, string? comment = null, CancellationToken ct = default)
    {
        ValidateOrderId(orderId);
        if (reviewId == Guid.Empty) throw new ArgumentException("Review id cannot be empty.", nameof(reviewId));
        resolvedBy = Required(resolvedBy, 200, nameof(resolvedBy));
        comment = string.IsNullOrWhiteSpace(comment) ? null : Required(comment, 4000, nameof(comment));

        await using var db = _databaseFactory.GetDatabase();
        await db.OrderReviews.Where(x => x.OrderId == orderId && x.Id == reviewId && x.ResolvedUtc == null)
            .Set(x => x.ResolvedUtc, (DateTime?)DateTime.UtcNow)
            .Set(x => x.ResolvedBy, resolvedBy)
            .Set(x => x.ResolutionComment, comment)
            .UpdateAsync(ct).ConfigureAwait(false);
        return await db.OrderReviews.FirstOrDefaultAsync(x => x.OrderId == orderId && x.Id == reviewId, ct).ConfigureAwait(false);
    }

    private static void ValidateOrderId(Guid orderId)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("Order id cannot be empty.", nameof(orderId));
    }

    private static string Required(string value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value cannot be empty.", name);
        value = value.Trim();
        if (value.Length > maxLength) throw new ArgumentException($"Value cannot exceed {maxLength} characters.", name);
        return value;
    }
}
