using Ekom.Printer.Agent.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace Ekom.Printer.Agent;

internal sealed class SetupPipeServer(AgentRuntime runtime, ILogger<SetupPipeServer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), PipeAccessRights.ReadWrite, AccessControlType.Allow));
            security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User!, PipeAccessRights.FullControl, AccessControlType.Allow));
            // Deny network logons even if they belong to Administrators: setup is local only.
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null), PipeAccessRights.FullControl, AccessControlType.Deny));
            using var pipe = NamedPipeServerStreamAcl.Create(AgentSetupProtocol.PipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 4096, 4096, security);
            try
            {
                await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromMinutes(2));
                var bytes = new List<byte>();
                var buffer = new byte[1];
                while (bytes.Count < AgentSetupProtocol.MaximumMessageBytes && await pipe.ReadAsync(buffer, timeout.Token).ConfigureAwait(false) != 0)
                {
                    if (buffer[0] == (byte)'\n') break;
                    bytes.Add(buffer[0]);
                }
                if (bytes.Count == AgentSetupProtocol.MaximumMessageBytes) throw new InvalidDataException("Setup message too large.");
                var request = JsonSerializer.Deserialize<AgentSetupRequest>(bytes.ToArray()) ?? throw new InvalidDataException("Empty setup request.");
                AgentSetupResponse response;
                try { response = await runtime.ExecuteAsync(request, timeout.Token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning("Setup operation failed ({ErrorType}).", ex.GetType().Name);
                    response = new AgentSetupResponse(false, "Operation failed. Check the service account, installed printer and Windows Application event log. Do not repeat a test print until its physical outcome is checked.");
                }
                await JsonSerializer.SerializeAsync(pipe, response, cancellationToken: timeout.Token).ConfigureAwait(false);
                await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is IOException or JsonException or OperationCanceledException)
            {
                logger.LogWarning("Local setup connection ended ({ErrorType}).", ex.GetType().Name);
            }
        }
    }
}
