using Ekom.Printer.Configuration;
using Ekom.Printer.Protocol;
using Ekom.Printer.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ekom.Printer.Api;

public static class PrinterEndpointRouteBuilderExtensions
{
    /// <summary>Map station-authenticated HTTPS endpoints. Configure forwarded headers at the host if TLS terminates upstream.</summary>
    public static IEndpointRouteBuilder MapEkomPrinter(this IEndpointRouteBuilder endpoints)
    {
        PrinterManagerEndpoints.Map(endpoints);
        // Explicit station authentication replaces any CMS cookie/fallback policy on these endpoints.
        var group = endpoints.MapGroup("/ekom/printer/v1/stations/{station}")
            .AllowAnonymous().AddEndpointFilter<StationAccessFilter>();
        group.MapPost("/claim", async (HttpContext context, PrinterDeliveryStore store, CancellationToken ct) =>
        {
            var job = await store.ClaimAsync(Station(context).Alias, ct).ConfigureAwait(false);
            return job == null ? Results.NoContent() : Results.Ok(job);
        });
        group.MapPost("/jobs/{jobId}/acknowledge", async (string jobId, PrintAcknowledgement acknowledgement,
            HttpContext context, PrinterDeliveryStore store, CancellationToken ct) =>
        {
            if (jobId.Length != 64 || !jobId.All(Uri.IsHexDigit) || acknowledgement.ClaimToken == Guid.Empty ||
                acknowledgement.SpoolJobId?.Length > 100 ||
                (acknowledgement.Submitted && string.IsNullOrWhiteSpace(acknowledgement.SpoolJobId)))
                return Results.BadRequest("Invalid acknowledgement.");
            var accepted = await store.AcknowledgeAsync(Station(context).Alias, jobId, acknowledgement, ct).ConfigureAwait(false);
            return accepted ? Results.NoContent() : Results.Conflict();
        });
        group.MapPost("/heartbeat", async (AgentHeartbeat heartbeat, HttpContext context, PrinterDeliveryStore store, CancellationToken ct) =>
        {
            if (!PrinterProtocol.IsCompatible(heartbeat.ProtocolVersion))
                return Results.Json(AgentCompatibility.Check(heartbeat.ProtocolVersion), statusCode: StatusCodes.Status426UpgradeRequired);
            if (string.IsNullOrWhiteSpace(heartbeat.AgentVersion) || heartbeat.AgentVersion.Length > 100 ||
                string.IsNullOrWhiteSpace(heartbeat.PrinterName) || heartbeat.PrinterName.Length > 255)
                return Results.BadRequest("Agent version and printer name are required.");
            await store.HeartbeatAsync(Station(context).Alias, heartbeat, ct).ConfigureAwait(false);
            return Results.Ok(AgentCompatibility.Check(heartbeat.ProtocolVersion));
        });
        group.MapGet("/health", async (HttpContext context, PrinterDeliveryStore store, CancellationToken ct) =>
            Results.Ok(await store.GetHealthAsync(Station(context).Alias, ct).ConfigureAwait(false)));
        return endpoints;
    }

    private static PrinterStationOptions Station(HttpContext context)
        => (PrinterStationOptions)(context.Items[typeof(PrinterStationOptions)]
            ?? throw new InvalidOperationException("Station authentication did not run."));
}
