using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace Ekom.Mailchimp.Exceptions;

[SuppressMessage("Design", "CA1032", Justification = "Mailchimp Transactional API context is required.")]
[SuppressMessage("Design", "CA1064", Justification = "Only the internal dispatcher observes this exception.")]
internal sealed class MailchimpTransactionalApiException : Exception
{
    public MailchimpTransactionalApiException(
        HttpStatusCode statusCode,
        string path,
        string? errorName,
        string? errorMessage,
        TimeSpan? retryAfter = null)
        : base($"Mailchimp Transactional request to '{path}' failed with status {(int)statusCode}"
            + (string.IsNullOrWhiteSpace(errorName) ? "." : $" ({errorName})."))
    {
        StatusCode = statusCode;
        Path = path;
        ErrorName = errorName;
        ErrorMessage = errorMessage;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode StatusCode { get; }
    public string Path { get; }
    public string? ErrorName { get; }
    public string? ErrorMessage { get; }
    public TimeSpan? RetryAfter { get; }
    public bool IsTransient => StatusCode == HttpStatusCode.TooManyRequests || (int)StatusCode >= 500;
}
