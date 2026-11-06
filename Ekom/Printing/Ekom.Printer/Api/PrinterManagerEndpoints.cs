using Ekom.Printer.Configuration;
using Ekom.Printer.Services;
using Ekom.Printer.Storage;
using Ekom.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ekom.Printer.Api;

internal static class PrinterManagerEndpoints
{
    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ekom/printer/manager").RequireAuthorization("UmbracoUser")
            .AddEndpointFilter<ManagerAccessFilter>();
        group.MapGet("/capabilities", (HttpContext http, PrinterManagerService manager, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { enabled = true, canManage = manager.CanManage,
                csrfHeader = tokens.HeaderName, csrfToken = tokens.RequestToken });
        });
        group.MapGet("/jobs", async (string? state, string? station, string? search, int? page,
            PrinterManagerService manager, CancellationToken ct) =>
        {
            if (page is < 1 or > 100000 || search?.Length > 100 || station?.Length > 100 ||
                (!string.IsNullOrEmpty(state) && state != "attention" && !Enum.TryParse<PrintJobState>(state, out _)))
                return Results.BadRequest();
            return Results.Ok(await manager.ListAsync(state, station, search, page ?? 1, ct).ConfigureAwait(false));
        });
        group.MapGet("/stations", async (IOptions<PrinterOptions> options, IManagerAccessService access,
            PrinterDeliveryStore delivery, CancellationToken ct) =>
        {
            var stations = new List<object>();
            foreach (var station in options.Value.Stations)
            {
                var stores = station.StoreAliases.Where(access.CanAccessStore).ToArray();
                if (stores.Length == 0) continue;
                stations.Add(new { alias = station.Alias, stores,
                    triggerMode = station.PickingSlip.TriggerMode.ToString(),
                    statuses = station.PickingSlip.TriggerStatuses.Select(x => x.ToString()).ToArray(),
                    policy = station.PickingSlip.PrintPolicy.ToString(), copies = station.PickingSlip.Copies,
                    enabled = station.PickingSlip.Enabled,
                    health = await delivery.GetHealthAsync(station.Alias, ct).ConfigureAwait(false) });
            }
            return Results.Ok(stations);
        });
        group.MapGet("/jobs/{id}/preview", async (string id, PrinterManagerService manager, CancellationToken ct) =>
        {
            var job = await manager.FindAsync(id, ct).ConfigureAwait(false);
            // Never serve customer HTML as a same-origin HTML document. The UI sanitizes it in a sandbox.
            return job?.Html == null ? Results.NotFound() : Results.Json(new { html = job.Html });
        });
        group.MapGet("/jobs/{id}/history", async (string id, PrinterManagerService manager, CancellationToken ct) =>
        {
            var history = await manager.HistoryAsync(id, ct).ConfigureAwait(false);
            return history == null ? Results.NotFound() : Results.Ok(history);
        });
        group.MapPost("/jobs/{id}/actions", async (string id, PrinterManagerRequest request,
            HttpContext http, PrinterManagerService manager, IAntiforgery antiforgery, CancellationToken ct) =>
        {
            if (!manager.CanManage) return Results.StatusCode(StatusCodes.Status403Forbidden);
            try { await antiforgery.ValidateRequestAsync(http).ConfigureAwait(false); }
            catch (AntiforgeryValidationException) { return Results.BadRequest("Invalid anti-forgery token. Reload Printing."); }
            if (id.Length != 64 || request.RequestId == Guid.Empty || request.Action is not ("retry" or "cancel" or "reprint" or "resolve-printed" or "resolve-reprint"))
                return Results.BadRequest();
            var actor = http.User.Identity?.Name ?? http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Backoffice user";
            if (actor.Length > 255) actor = actor[..255];
            var result = await manager.ActAsync(id, request, actor, ct).ConfigureAwait(false);
            return result == null ? Results.Conflict("The job is unavailable or its state changed. Refresh the list.") : Results.Ok(new { jobId = result });
        });
    }

    private sealed class ManagerAccessFilter(IOptions<PrinterOptions> options, IManagerAccessService access) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (!options.Value.Enabled) return Results.NotFound();
            if (!access.CanAccessManager()) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return await next(context).ConfigureAwait(false);
        }
    }
}
