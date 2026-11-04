using Ekom.Printer.Protocol;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Printer.Agent.Client;

/// <summary>Station-scoped client. The caller owns HttpClient and must disable automatic redirects.</summary>
public sealed class PrinterAgentClient
{
    /// <summary>Create the recommended transport. Caller owns and disposes the returned client.</summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "The returned HttpClient owns its handler and the caller disposes the client.")]
    public static HttpClient CreateHttpClient() => new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        CheckCertificateRevocationList = true,
    }, disposeHandler: true);

    private readonly HttpClient _http;
    private readonly Uri _stationUri;
    private readonly string _token;

    public PrinterAgentClient(HttpClient http, Uri serverUri, string stationAlias, string token)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(serverUri);
        if (!serverUri.IsAbsoluteUri || serverUri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(serverUri.UserInfo) || !string.IsNullOrEmpty(serverUri.Query) || !string.IsNullOrEmpty(serverUri.Fragment))
            throw new ArgumentException("An absolute HTTPS server URL without credentials, query or fragment is required.", nameof(serverUri));
        if (string.IsNullOrWhiteSpace(stationAlias)) throw new ArgumentException("A station alias is required.", nameof(stationAlias));
        if (string.IsNullOrWhiteSpace(token) || token.Length is < 32 or > 512)
            throw new ArgumentException("A random station token of 32-512 characters is required.", nameof(token));
        _http = http;
        _token = token;
        _stationUri = new Uri(serverUri.AbsoluteUri.TrimEnd('/') + "/ekom/printer/v1/stations/" + Uri.EscapeDataString(stationAlias) + "/");
    }

    public async Task<ClaimedPrintJob?> ClaimAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync("claim", null, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        return await response.Content.ReadFromJsonAsync<ClaimedPrintJob>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Server returned an empty print job.");
    }

    public async Task AcknowledgeAsync(string jobId, PrintAcknowledgement acknowledgement, CancellationToken cancellationToken = default)
    {
        if (jobId.Length != 64 || !jobId.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid job identifier.", nameof(jobId));
        using var response = await SendAsync("jobs/" + jobId + "/acknowledge", JsonContent.Create(acknowledgement), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task HeartbeatAsync(string agentVersion, string printerName, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync("heartbeat", JsonContent.Create(new AgentHeartbeat(PrinterProtocol.CurrentVersion,
            agentVersion, printerName)), cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_stationUri, path)) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.Add(PrinterProtocol.VersionHeader, PrinterProtocol.CurrentVersion.ToString(CultureInfo.InvariantCulture));
        var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.UpgradeRequired)
        {
            response.Dispose();
            throw new InvalidOperationException("Printer agent and server are incompatible. Install compatible versions before printing.");
        }
        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new HttpRequestException("Printer server rejected the request.", null, status);
        }
        return response;
    }
}
