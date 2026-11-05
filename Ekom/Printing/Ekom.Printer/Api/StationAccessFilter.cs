using Ekom.Printer.Configuration;
using Ekom.Printer.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Printer.Api;

internal sealed class StationAccessFilter(IOptions<PrinterOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        if (!options.Value.Enabled) return Results.NotFound();
        if (!http.Request.IsHttps) return Results.StatusCode(StatusCodes.Status400BadRequest);
        var alias = http.Request.RouteValues["station"]?.ToString();
        var station = options.Value.Stations.FirstOrDefault(x => string.Equals(x.Alias, alias, StringComparison.OrdinalIgnoreCase));
        if (station == null || !Authenticate(http.Request.Headers.Authorization.ToString(), station.TokenSha256))
            return Results.Unauthorized();
        if (!int.TryParse(http.Request.Headers[PrinterProtocol.VersionHeader], NumberStyles.None,
            CultureInfo.InvariantCulture, out var version)) return Results.BadRequest("Missing or invalid printer protocol header.");
        if (!PrinterProtocol.IsCompatible(version))
            return Results.Json(AgentCompatibility.Check(version), statusCode: StatusCodes.Status426UpgradeRequired);
        http.Items[typeof(PrinterStationOptions)] = station;
        return await next(context).ConfigureAwait(false);
    }

    internal static bool Authenticate(string authorization, string expectedHash)
    {
        const string prefix = "Bearer ";
        if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            authorization.Length is < 39 or > 519 || expectedHash.Length != 64) return false;
        byte[] expected;
        try { expected = Convert.FromHexString(expectedHash); }
        catch (FormatException) { return false; }
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(authorization[prefix.Length..]));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
