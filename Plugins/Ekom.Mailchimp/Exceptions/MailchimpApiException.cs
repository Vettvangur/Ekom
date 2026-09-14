using System.Net;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Mailchimp.Exceptions;

[SuppressMessage("Design", "CA1032", Justification = "Mailchimp API context is required to construct this exception.")]
public sealed class MailchimpApiException : Exception
{
    public MailchimpApiException(
        HttpStatusCode statusCode,
        string path,
        string? responseBody,
        TimeSpan? retryAfter = null)
        : base($"Mailchimp request to '{path}' failed with status {(int)statusCode}.")
    {
        StatusCode = statusCode;
        Path = path;
        ResponseBody = responseBody;
        RetryAfter = retryAfter;
    }

    public HttpStatusCode StatusCode { get; }
    public string Path { get; }
    public string? ResponseBody { get; }
    public TimeSpan? RetryAfter { get; }
    public bool IsTransient => StatusCode == HttpStatusCode.TooManyRequests || (int)StatusCode >= 500;
}
