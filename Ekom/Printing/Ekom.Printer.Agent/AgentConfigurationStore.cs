using Ekom.Printer.Agent.Client;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ekom.Printer.Agent;

internal sealed record StoredConfiguration(AgentSettings Settings, string ProtectedToken);

internal sealed class AgentConfigurationStore
{
    public string DataDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Ekom", "PrinterAgent");
    private string ConfigurationPath => Path.Combine(DataDirectory, "settings.json");

    public StoredConfiguration? Load() => File.Exists(ConfigurationPath)
        ? JsonSerializer.Deserialize<StoredConfiguration>(File.ReadAllText(ConfigurationPath))
            ?? throw new InvalidOperationException("Invalid agent configuration.")
        : null;

    public string GetToken(StoredConfiguration configuration)
    {
        var bytes = ProtectedData.Unprotect(Convert.FromBase64String(configuration.ProtectedToken), null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public void Save(AgentSettings settings, string? token)
    {
        settings.Validate();
        var previous = Load();
        if (string.IsNullOrEmpty(token))
        {
            if (previous == null || previous.Settings.ServerUrl != settings.ServerUrl || previous.Settings.StationAlias != settings.StationAlias)
                throw new ArgumentException("Enter the credential when configuring or changing the website or station.");
            token = GetToken(previous);
        }
        if (token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace))
            throw new ArgumentException("Enter a random station credential of 32-512 characters without whitespace.");
        if (previous != null && (previous.Settings.ServerUrl != settings.ServerUrl || previous.Settings.StationAlias != settings.StationAlias) &&
            Directory.Exists(Path.Combine(DataDirectory, "journal")) && Directory.EnumerateFiles(Path.Combine(DataDirectory, "journal"), "*.json").Any())
            throw new InvalidOperationException("Resolve pending submission reports before changing station identity.");
        var bytes = Encoding.UTF8.GetBytes(token);
        string encrypted;
        try { encrypted = Convert.ToBase64String(ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        // The MSI creates this directory with an explicit service/admin-only ACL.
        if (!Directory.Exists(DataDirectory)) throw new InvalidOperationException("Install the agent before configuring it.");
        var temporary = ConfigurationPath + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            JsonSerializer.Serialize(stream, new StoredConfiguration(settings, encrypted));
            stream.Flush(true);
        }
        File.Move(temporary, ConfigurationPath, true);
    }
}
