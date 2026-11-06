using Ekom.Utilities;
using Microsoft.Extensions.Options;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Printer.Configuration;

[SuppressMessage("Usage", "CA2227", Justification = "Configuration binding and programmatic options support collection replacement.")]
public sealed class PrinterOptions
{
    public const string SectionName = "Ekom:Printing";
    public bool Enabled { get; set; }
    public Collection<PrinterStationOptions> Stations { get; set; } = [];
    public int HtmlRetentionDays { get; set; } = 30;
    public int HistoryRetentionDays { get; set; } = 90;
    public int OfflineAfterSeconds { get; set; } = 120;
    public int ClaimTimeoutSeconds { get; set; } = 300;
    /// <summary>Backoffice groups allowed to retry, cancel and reprint. Administrators always have access.</summary>
    public Collection<string> ManagerGroups { get; set; } = [];
}

[SuppressMessage("Usage", "CA2227", Justification = "Configuration options support collection replacement.")]
public sealed class PrinterStationOptions
{
    public string Alias { get; set; } = string.Empty;
    /// <summary>Hex SHA-256 hash of a random station bearer token. Never store the raw token here.</summary>
    public string TokenSha256 { get; set; } = string.Empty;
    public Collection<string> StoreAliases { get; set; } = [];
    public PickingSlipOptions PickingSlip { get; set; } = new();
}

[SuppressMessage("Usage", "CA2227", Justification = "Configuration options support collection replacement.")]
public sealed class PickingSlipOptions
{
    public bool Enabled { get; set; } = true;
    public PrinterTriggerMode TriggerMode { get; set; } = PrinterTriggerMode.StatusChange;
    public Collection<OrderStatus> TriggerStatuses { get; set; } = [];
    public PrintPolicy PrintPolicy { get; set; } = PrintPolicy.OncePerOrder;
    public int Copies { get; set; } = 1;
}

public enum PrinterTriggerMode
{
    StatusChange,
    ExplicitCapture,
}

public enum PrintPolicy
{
    OncePerOrder,
    OncePerStatus,
}

internal sealed class PrinterOptionsValidator : IValidateOptions<PrinterOptions>
{
    public ValidateOptionsResult Validate(string? name, PrinterOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;
        var errors = new List<string>();
        if (options.HtmlRetentionDays < 1 || options.HistoryRetentionDays < options.HtmlRetentionDays)
            errors.Add("Retention must be positive and history retention must be at least HTML retention.");
        if (options.OfflineAfterSeconds < 30) errors.Add("OfflineAfterSeconds must be at least 30.");
        if (options.ClaimTimeoutSeconds is < 30 or > 3600) errors.Add("ClaimTimeoutSeconds must be between 30 and 3600.");
        if (options.Stations.Count == 0) errors.Add("At least one print station is required.");
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var station in options.Stations)
        {
            if (string.IsNullOrWhiteSpace(station.Alias) || station.Alias.Length > 100 ||
                station.Alias != station.Alias.Trim() || !aliases.Add(station.Alias))
                errors.Add("Station aliases must be unique, nonempty, trimmed and at most 100 characters.");
            if (station.StoreAliases.Count == 0 || station.StoreAliases.Any(string.IsNullOrWhiteSpace))
                errors.Add($"Station {station.Alias} requires store aliases.");
            if (station.TokenSha256.Length != 64 || !station.TokenSha256.All(Uri.IsHexDigit))
                errors.Add($"Station {station.Alias} requires a 64-character SHA-256 credential hash.");
            if (!ValidSlip(station.PickingSlip))
                errors.Add($"Station {station.Alias} has invalid picking-slip settings. ExplicitCapture requires OncePerOrder and no TriggerStatuses; enabled StatusChange requires statuses.");
        }
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool ValidSlip(PickingSlipOptions slip) =>
        slip.Copies is >= 1 and <= 100 && Enum.IsDefined(slip.PrintPolicy) && Enum.IsDefined(slip.TriggerMode) &&
        slip.TriggerStatuses.All(Enum.IsDefined) &&
        (slip.TriggerMode == PrinterTriggerMode.ExplicitCapture
            ? slip.TriggerStatuses.Count == 0 && slip.PrintPolicy == PrintPolicy.OncePerOrder
            : !slip.Enabled || slip.TriggerStatuses.Count > 0);
}
