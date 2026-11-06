using Ekom.Printer.Agent.Client;
using System.Net;
using Xunit;

namespace Ekom.Printer.Agent.Tests;

public sealed class ConnectionDiagnosticsTests
{
    [Theory]
    [InlineData(404, "enable Ekom:Printing:Enabled")]
    [InlineData(401, "original token")]
    [InlineData(403, "access rules")]
    [InlineData(426, "incompatible")]
    [InlineData(302, "final HTTPS")]
    [InlineData(500, "migration")]
    [InlineData(429, "rate limits")]
    public void HttpErrorsExplainRecoveryWithoutEchoingSensitiveContent(int status, string expected)
    {
        var message = ConnectionDiagnostics.Describe(new HttpRequestException("secret-token-response", null, (HttpStatusCode)status));
        Assert.Contains(expected, message, StringComparison.Ordinal);
        Assert.Contains(status.ToString(), message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-response", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, "DNS")]
    [InlineData(HttpRequestError.SecureConnectionError, "certificate")]
    [InlineData(HttpRequestError.ConnectionError, "Windows service account")]
    public void TransportErrorsExplainRecovery(HttpRequestError error, string expected)
    {
        Assert.Contains(expected, ConnectionDiagnostics.Describe(new HttpRequestException(error)), StringComparison.Ordinal);
    }
}
