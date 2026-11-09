namespace Ekom.Printer.Protocol;

/// <summary>Versions are independent of NuGet versions.</summary>
public static class PrinterProtocol
{
    public const int CurrentVersion = 1;
    public const string VersionHeader = "X-Ekom-Printer-Protocol";

    public static bool IsCompatible(int agentProtocolVersion) => agentProtocolVersion == CurrentVersion;
}

/// <summary>Required renderer policy. The Windows agent must enforce this in its rendering engine.</summary>
public sealed record HtmlPrintPolicy
{
    public bool JavaScriptEnabled => false;
    public bool ExternalNetworkRequestsEnabled => false;
    public bool LocalFileAccessEnabled => false;
}

public sealed record AgentHeartbeat(int ProtocolVersion, string AgentVersion, string PrinterName);

public sealed record ClaimedPrintJob(string JobId, Guid ClaimToken, DateTime ExpiresUtc,
    string Html, int Copies, HtmlPrintPolicy Policy);

/// <summary>Submitted means accepted by Windows, not confirmed physical printing.</summary>
public sealed record PrintAcknowledgement(Guid ClaimToken, bool Submitted, string? SpoolJobId);

public sealed record StationHealth(string StationAlias, DateTime? LastSeenUtc, bool Online,
    bool Compatible, string? AgentVersion, string? PrinterName);

public sealed record AgentCompatibility(bool Compatible, int RequiredProtocolVersion, string? Message)
{
    public static AgentCompatibility Check(int version) => PrinterProtocol.IsCompatible(version)
        ? new(true, PrinterProtocol.CurrentVersion, null)
        : new(false, PrinterProtocol.CurrentVersion, "Agent and server protocol versions differ. Install compatible versions before printing.");
}
