using Ekom.Printer.Agent;
using Ekom.Printer.Agent.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

// Called by the elevated installer before the low-privilege service is started.
if (args is ["--register-event-source"])
{
    if (!EventLog.SourceExists(AgentSetupProtocol.ServiceName))
        EventLog.CreateEventSource(AgentSetupProtocol.ServiceName, "Application");
    return;
}

// Bundled, version-matched Chromium; never download binaries on a warehouse computer.
Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", Path.Combine(AppContext.BaseDirectory, "browsers"));
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options => options.ServiceName = AgentSetupProtocol.ServiceName);
builder.Logging.AddEventLog(options => options.SourceName = AgentSetupProtocol.ServiceName);
builder.Services.AddSingleton<AgentConfigurationStore>();
builder.Services.AddSingleton<AgentRuntime>();
builder.Services.AddHostedService<PrintWorker>();
builder.Services.AddHostedService<SetupPipeServer>();
await builder.Build().RunAsync().ConfigureAwait(false);
