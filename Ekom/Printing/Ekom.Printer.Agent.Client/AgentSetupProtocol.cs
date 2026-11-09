namespace Ekom.Printer.Agent.Client;

/// <summary>Local administrator-to-service protocol; never exposed over TCP.</summary>
public static class AgentSetupProtocol
{
    public const string PipeName = "Ekom.Printer.Agent.Setup.v1";
    public const string ServiceName = "EkomPrinterAgent";
    public const int MaximumMessageBytes = 16384;
}

public sealed record AgentSettings(string ServerUrl, string StationAlias, string PrinterName, bool Enabled)
{
    public void Validate()
    {
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Enter an HTTPS website URL without credentials, query or fragment.");
        if (string.IsNullOrWhiteSpace(StationAlias) || StationAlias.Length > 100 || StationAlias != StationAlias.Trim())
            throw new ArgumentException("Enter a station alias of at most 100 characters without surrounding spaces.");
        if (string.IsNullOrWhiteSpace(PrinterName) || PrinterName.Length > 255)
            throw new ArgumentException("Select a printer available to the Windows service.");
    }
}

public sealed record AgentSetupRequest(string Command, AgentSettings? Settings = null, string? Token = null);
public sealed record AgentSetupResponse(bool Success, string Message, AgentSettings? Settings = null,
    string[]? Printers = null, string? ServiceIdentity = null, DateTimeOffset? LastHeartbeatUtc = null);
