using Ekom.ActionFilters;
using Ekom.Analytics;
using Ekom.Authorization;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Controllers;

/// <summary>Store-authorized reporting and maintenance; no order-view or checkout integration.</summary>
[Route("ekom/manager/analytics")]
[CamelCaseJson]
[UmbracoUserAuthorize]
[Authorize(Policy = AnalyticsAuthorization.Policy)]
public sealed class EkomAnalyticsController : ControllerBase
{
    private readonly IManagerAccessService _access;
    private readonly AnalyticsReportRepository _reports;
    private readonly AnalyticsProjectionService _projection;
    private readonly AnalyticsJobService _jobs;
    private readonly DatabaseFactory _database;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<EkomAnalyticsController> _logger;

    public EkomAnalyticsController(IManagerAccessService access, AnalyticsReportRepository reports,
        AnalyticsProjectionService projection, AnalyticsJobService jobs, DatabaseFactory database,
        IOptions<AnalyticsOptions> options, ILogger<EkomAnalyticsController> logger)
    {
        _access = access;
        _reports = reports;
        _projection = projection;
        _jobs = jobs;
        _database = database;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet("sales")]
    public Task<IActionResult> Sales([FromQuery] AnalyticsReportFilter filter, CancellationToken ct)
        => ReportAsync(filter, () => _reports.SalesAsync(filter, ct));

    [HttpGet("daily-sales")]
    public Task<IActionResult> DailySales([FromQuery] AnalyticsReportFilter filter, CancellationToken ct)
        => ReportAsync(filter, () => _reports.DailySalesAsync(filter, ct));

    [HttpGet("products")]
    public Task<IActionResult> Products([FromQuery] AnalyticsReportFilter filter, bool variants = false,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
        => ReportAsync(filter, () => _reports.ProductsAsync(filter, variants, page, pageSize, ct));

    [HttpGet("distributions/{dimension}")]
    public Task<IActionResult> Distribution(string dimension, [FromQuery] AnalyticsReportFilter filter, CancellationToken ct)
        => ReportAsync(filter, () => _reports.DistributionAsync(filter, dimension, ct));

    [HttpGet("promotions")]
    public Task<IActionResult> Promotions([FromQuery] AnalyticsReportFilter filter, int page = 1,
        int pageSize = 20, CancellationToken ct = default)
        => ReportAsync(filter, () => _reports.PromotionsAsync(filter, page, pageSize, ct));

    [HttpGet("orders")]
    public Task<IActionResult> Orders([FromQuery] AnalyticsReportFilter filter, string? customerIdentityKey = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default)
        => ReportAsync(filter, () => _reports.OrdersAsync(filter, customerIdentityKey, page, pageSize, ct));

    [HttpPost("orders/{orderId:guid}/refresh")]
    public async Task<IActionResult> RefreshOrder(Guid orderId, CancellationToken ct = default)
    {
        if (!_access.CanAccessManager()) return Forbid();
        if (!Available()) return Unavailable();
        try
        {
            using var db = _database.GetDatabase();
            db.CommandTimeout = _options.DatabaseCommandTimeoutSeconds;
            var store = await db.GetTable<OrderData>().Where(x => x.UniqueId == orderId)
                .Select(x => x.StoreAlias).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (store == null) return NotFound();
            if (!_access.CanAccessStore(store)) return Forbid();
            var result = await _projection.RefreshAsync(orderId, ct).ConfigureAwait(false);
            return result.Success ? Ok(result) : StatusCode(503, result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual analytics refresh failed. OrderId: {OrderId}", orderId);
            return Unavailable();
        }
    }

    [HttpPost("rebuilds")]
    public async Task<IActionResult> StartRebuild([FromBody] AnalyticsRebuildRequest? request, CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Store)) return BadRequest("Store is required.");
        if (!_access.CanAccessManager() || !_access.CanAccessStore(request.Store)) return Forbid();
        if (!Available()) return Unavailable();
        try
        {
            var job = await _jobs.StartRebuildAsync(request.Store, ct).ConfigureAwait(false);
            return AcceptedAtAction(nameof(GetJob), new { jobId = job.JobId }, job);
        }
        catch (ArgumentException) { return BadRequest("Invalid rebuild request."); }
        catch (InvalidOperationException) { return Conflict("A rebuild cannot be started. Check analytics readiness and existing jobs."); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start analytics rebuild for store {StoreAlias}", request.Store);
            return Unavailable();
        }
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> ListJobs(string store, CancellationToken ct = default)
    {
        if (!_access.CanAccessManager() || !_access.CanAccessStore(store)) return Forbid();
        if (string.IsNullOrWhiteSpace(store) || store.Length > 100) return BadRequest("A valid store is required.");
        if (!Available()) return Unavailable();
        try
        {
            return Ok(await _jobs.ListAsync(store, ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list analytics jobs for store {StoreAlias}", store);
            return Unavailable();
        }
    }

    [HttpGet("jobs/{jobId:guid}")]
    public async Task<IActionResult> GetJob(Guid jobId, CancellationToken ct = default)
    {
        if (!_access.CanAccessManager()) return Forbid();
        if (!Available()) return Unavailable();
        try
        {
            var job = await _jobs.GetAsync(jobId, ct).ConfigureAwait(false);
            if (job == null) return NotFound();
            if (!_access.CanAccessStore(job.StoreAlias)) return Forbid();
            return Ok(job);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read analytics job {JobId}", jobId);
            return Unavailable();
        }
    }

    [HttpPost("jobs/{jobId:guid}/control")]
    public async Task<IActionResult> ControlJob(Guid jobId, [FromBody] AnalyticsJobControlRequest? request,
        CancellationToken ct = default)
    {
        if (!_access.CanAccessManager()) return Forbid();
        if (!Available()) return Unavailable();
        if (request == null || (request.Command != "pause" && request.Command != "resume" && request.Command != "cancel"))
            return BadRequest("Command must be pause, resume or cancel.");
        try
        {
            var job = await _jobs.GetAsync(jobId, ct).ConfigureAwait(false);
            if (job == null) return NotFound();
            if (!_access.CanAccessStore(job.StoreAlias)) return Forbid();
            return await _jobs.ControlAsync(jobId, request.Command, ct).ConfigureAwait(false)
                ? Ok() : Conflict("The job cannot accept that command in its current state.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to control analytics job {JobId}", jobId);
            return Unavailable();
        }
    }

    private async Task<IActionResult> ReportAsync<T>(AnalyticsReportFilter filter, Func<Task<T>> query)
    {
        if (!_access.CanAccessManager() || !_access.CanAccessStore(filter.Store)) return Forbid();
        if (!Available()) return Unavailable();
        try
        {
            filter.Validate();
            return Ok(await query().ConfigureAwait(false));
        }
        catch (ArgumentException) { return BadRequest("Invalid analytics filter or pagination."); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics query failed for store {StoreAlias}", filter.Store);
            return Unavailable();
        }
    }

    private bool Available() => _options.Enabled && _options.IsValid(out _);
    private ObjectResult Unavailable() => StatusCode(503, "Analytics is disabled or unavailable.");
}

public sealed record AnalyticsRebuildRequest(string Store);
public sealed record AnalyticsJobControlRequest(string Command);
