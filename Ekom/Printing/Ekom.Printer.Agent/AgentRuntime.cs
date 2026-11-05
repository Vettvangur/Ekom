using Ekom.Printer.Agent.Client;
using Ekom.Printer.Protocol;
using System.Drawing.Printing;
using System.Security.Principal;

namespace Ekom.Printer.Agent;

internal sealed class AgentRuntime(AgentConfigurationStore store) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _http = CreateHttp();
    private DateTimeOffset? _lastHeartbeat;
    private string _status = "Waiting for configuration.";

    public async Task PollAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var config = store.Load();
            if (config == null || !config.Settings.Enabled) return;
            config.Settings.Validate();
            var client = CreateClient(config);
            if (_lastHeartbeat == null || DateTimeOffset.UtcNow - _lastHeartbeat > TimeSpan.FromSeconds(30))
            {
                await client.HeartbeatAsync(typeof(AgentRuntime).Assembly.GetName().Version!.ToString(), config.Settings.PrinterName, ct).ConfigureAwait(false);
                _lastHeartbeat = DateTimeOffset.UtcNow;
            }
            var processor = new AgentPrintProcessor(client, new SubmissionJournal(Path.Combine(store.DataDirectory, "journal")),
                new WindowsHtmlPrinter(config.Settings.PrinterName), TimeProvider.System);
            _status = await processor.ProcessNextAsync(ct).ConfigureAwait(false) ? "Job submitted and reported." : "Connected. Waiting for jobs.";
        }
        catch { _status = "Printing paused by an error. Check the Windows Application event log; uncertain jobs are not printed again."; throw; }
        finally { _gate.Release(); }
    }

    public async Task<AgentSetupResponse> ExecuteAsync(AgentSetupRequest request, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            switch (request.Command)
            {
                case "status": break;
                case "save":
                    var settings = request.Settings ?? throw new ArgumentException("Settings are required.");
                    if (!GetPrinters().Contains(settings.PrinterName, StringComparer.Ordinal))
                        throw new ArgumentException("This printer is not available to the Windows service account.");
                    store.Save(settings, request.Token);
                    _lastHeartbeat = null;
                    _status = settings.Enabled ? "Settings saved. Automatic printing enabled." : "Settings saved. Automatic printing paused.";
                    break;
                case "connection":
                    var connection = store.Load() ?? throw new InvalidOperationException("Save settings first.");
                    await CreateClient(connection).HeartbeatAsync(typeof(AgentRuntime).Assembly.GetName().Version!.ToString(), connection.Settings.PrinterName, ct).ConfigureAwait(false);
                    _lastHeartbeat = DateTimeOffset.UtcNow;
                    _status = "Website connection and protocol verified.";
                    break;
                case "test":
                    var config = store.Load() ?? throw new InvalidOperationException("Save settings first.");
                    var spoolId = await new WindowsHtmlPrinter(config.Settings.PrinterName).SubmitAsync(
                        "<!doctype html><html><head><style>@page{size:A4;margin:15mm}body{font-family:sans-serif}</style></head><body>" +
                        "<h1>Ekom Printer Agent</h1><p>Service-account test print</p><p>Check text, page margins and printer selection.</p></body></html>",
                        1, new HtmlPrintPolicy(), ct).ConfigureAwait(false);
                    _status = "Test submitted to Windows. Queue reference: " + spoolId + ". Confirm paper output before enabling automatic printing.";
                    break;
                default: throw new ArgumentException("Unknown setup command.");
            }
            return new AgentSetupResponse(true, _status, store.Load()?.Settings, GetPrinters(), WindowsIdentity.GetCurrent().Name, _lastHeartbeat);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or HttpRequestException)
        {
            // Never return raw HTTP bodies, tokens or printer document content to diagnostics.
            return new AgentSetupResponse(false, ex is HttpRequestException http
                ? $"Website connection failed (HTTP {http.StatusCode?.ToString() ?? "unavailable"}). Check the URL and credential."
                : ex.Message);
        }
        finally { _gate.Release(); }
    }

    private PrinterAgentClient CreateClient(StoredConfiguration config)
    {
        return new PrinterAgentClient(_http, new Uri(config.Settings.ServerUrl), config.Settings.StationAlias, store.GetToken(config));
    }

    private static HttpClient CreateHttp()
    {
        var client = PrinterAgentClient.CreateHttpClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static string[] GetPrinters() => PrinterSettings.InstalledPrinters.Cast<string>().Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public void Dispose() { _http.Dispose(); _gate.Dispose(); }
}
