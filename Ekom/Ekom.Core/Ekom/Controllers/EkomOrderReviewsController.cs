using System.Diagnostics.CodeAnalysis;
using Ekom.ActionFilters;
using Ekom.Authorization;
using Ekom.Models.Manager;
using Ekom.Services;
using LinqToDB;
using Microsoft.AspNetCore.Mvc;

namespace Ekom.Controllers;

[Route("ekom/manager/orders/{orderId:guid}/reviews")]
[CamelCaseJson]
[UmbracoUserAuthorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EkomOrderReviewsController : ControllerBase
{
    private readonly IOrderReviewService _reviews;
    private readonly IManagerAccessService _access;
    private readonly DatabaseFactory _databaseFactory;

    public EkomOrderReviewsController(IOrderReviewService reviews, IManagerAccessService access, DatabaseFactory databaseFactory)
    {
        _reviews = reviews;
        _access = access;
        _databaseFactory = databaseFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(Guid orderId, bool includeResolved = false, int skip = 0, int take = 50, CancellationToken ct = default)
    {
        var denied = await CheckAccessAsync(orderId, ct).ConfigureAwait(false);
        if (denied != null) return denied;
        if (skip < 0 || take < 1 || take > 200) return BadRequest(new { message = "Skip must be non-negative and take must be between 1 and 200." });
        return Ok(await _reviews.GetReviewsAsync(orderId, includeResolved, skip, take, ct).ConfigureAwait(false));
    }

    [SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "Raises a persisted review issue, not a CLR event.")]
    [HttpPost]
    public async Task<IActionResult> RaiseAsync(Guid orderId, [FromBody] RaiseOrderReviewRequest? request, CancellationToken ct = default)
    {
        var denied = await CheckAccessAsync(orderId, ct).ConfigureAwait(false);
        if (denied != null) return denied;
        if (request == null || !ModelState.IsValid) return BadRequest();
        try
        {
            return Ok(await _reviews.RaiseAsync(orderId, request.Source, request.Code, request.Title, request.Message, ct).ConfigureAwait(false));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{reviewId:guid}/resolve")]
    public async Task<IActionResult> ResolveAsync(Guid orderId, Guid reviewId, [FromBody] ResolveOrderReviewRequest? request, CancellationToken ct = default)
    {
        var denied = await CheckAccessAsync(orderId, ct).ConfigureAwait(false);
        if (denied != null) return denied;
        if (request == null || !ModelState.IsValid) return BadRequest();
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return Unauthorized();
        try
        {
            var review = await _reviews.ResolveAsync(orderId, reviewId, userName, request.Comment, ct).ConfigureAwait(false);
            return review == null ? NotFound() : Ok(review);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<IActionResult?> CheckAccessAsync(Guid orderId, CancellationToken ct)
    {
        if (!_access.CanAccessManager()) return StatusCode(403);
        await using var db = _databaseFactory.GetDatabase();
        var order = await db.OrderData.Where(x => x.UniqueId == orderId)
            .Select(x => new { x.StoreAlias }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (order == null) return NotFound();
        return _access.CanAccessStore(order.StoreAlias) ? null : StatusCode(403);
    }
}
