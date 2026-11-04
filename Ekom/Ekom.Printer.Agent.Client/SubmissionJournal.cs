using Ekom.Printer.Protocol;
using System.Text.Json;

namespace Ekom.Printer.Agent.Client;

/// <summary>No HTML is retained. Protect this directory with service-account-only filesystem permissions.</summary>
public sealed class SubmissionJournal(string directory)
{
    internal FileStream AcquireLock()
    {
        Directory.CreateDirectory(directory);
        return new FileStream(Path.Combine(directory, "agent.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public async Task SaveAsync(SubmissionRecord record, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var target = GetPath(record.JobId);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, record, cancellationToken: cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public async Task<IReadOnlyList<SubmissionRecord>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory)) return Array.Empty<SubmissionRecord>();
        var records = new List<SubmissionRecord>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            await using var stream = File.OpenRead(path);
            var record = await JsonSerializer.DeserializeAsync<SubmissionRecord>(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Invalid submission journal; manual review is required.");
            if (!string.Equals(Path.GetFileName(path), record.JobId + ".json", StringComparison.Ordinal))
                throw new InvalidOperationException("Submission journal identity mismatch.");
            if (record.ClaimToken == Guid.Empty || (record.Result != null && record.Result.ClaimToken != record.ClaimToken))
                throw new InvalidOperationException("Submission journal claim mismatch.");
            records.Add(record);
        }
        return records;
    }

    public void Remove(string jobId) => File.Delete(GetPath(jobId));

    private string GetPath(string jobId)
    {
        if (jobId.Length != 64 || !jobId.All(Uri.IsHexDigit)) throw new ArgumentException("Invalid job identifier.", nameof(jobId));
        return Path.Combine(directory, jobId + ".json");
    }
}

/// <summary>A null result means submission may have started; recovery must not print it again.</summary>
public sealed record SubmissionRecord(string JobId, Guid ClaimToken, PrintAcknowledgement? Result);
