using Ekom.Printer.Protocol;

namespace Ekom.Printer.Agent.Client;

/// <summary>The Windows implementation must enforce the HTML policy and return the actual spool identifier.</summary>
public interface IHtmlPrinter
{
    Task<string> SubmitAsync(string html, int copies, HtmlPrintPolicy policy, CancellationToken cancellationToken = default);
}

/// <summary>Serializes consumers sharing the same journal directory. Configure one directory per station.</summary>
public sealed class AgentPrintProcessor(PrinterAgentClient client, SubmissionJournal journal, IHtmlPrinter printer, TimeProvider clock)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        using var journalLock = journal.AcquireLock();
        // Finish reporting old outcomes before asking for more work. Never replay physical printing.
        foreach (var previous in await journal.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var outcome = previous.Result ?? new PrintAcknowledgement(previous.ClaimToken, false, null);
            await client.AcknowledgeAsync(previous.JobId, outcome, cancellationToken).ConfigureAwait(false);
            journal.Remove(previous.JobId);
        }
        var job = await client.ClaimAsync(cancellationToken).ConfigureAwait(false);
        if (job == null) return false;
        if (job.ClaimToken == Guid.Empty || string.IsNullOrWhiteSpace(job.Html) || job.Copies is < 1 or > 100)
            throw new InvalidOperationException("Server returned an invalid print job; review the claimed job.");
        var record = new SubmissionRecord(job.JobId, job.ClaimToken, null);
        // Durable intent precedes any possible physical side effect.
        await journal.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        PrintAcknowledgement acknowledgement;
        if (job.ExpiresUtc <= clock.GetUtcNow().UtcDateTime)
        {
            acknowledgement = new PrintAcknowledgement(job.ClaimToken, false, null);
        }
        else
        {
            // An exception deliberately leaves the journal uncertain for recovery; it is never retried as a print.
            var spoolId = await printer.SubmitAsync(job.Html, job.Copies, job.Policy, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(spoolId) || spoolId.Length > 100)
                throw new InvalidOperationException("Printer returned an invalid spool identifier; review the print outcome.");
            acknowledgement = new PrintAcknowledgement(job.ClaimToken, true, spoolId);
        }
        await journal.SaveAsync(record with { Result = acknowledgement }, cancellationToken).ConfigureAwait(false);
        await client.AcknowledgeAsync(job.JobId, acknowledgement, cancellationToken).ConfigureAwait(false);
        journal.Remove(job.JobId);
        return true;
    }
}
