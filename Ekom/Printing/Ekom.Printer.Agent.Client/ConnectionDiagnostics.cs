using System.Net;

namespace Ekom.Printer.Agent.Client;

/// <summary>Actionable connection errors without response bodies, URLs or credentials.</summary>
public static class ConnectionDiagnostics
{
    public static string Describe(HttpRequestException error)
    {
        var advice = error.StatusCode switch
        {
            HttpStatusCode.NotFound => "Printer endpoint not found. Check the website URL, install Ekom.Printer on that website, enable Ekom:Printing:Enabled and map the printer endpoints.",
            HttpStatusCode.Unauthorized => "Station credential rejected. Check that the station alias matches appsettings and that the saved credential matches its TokenSha256. Use the original token in the agent, not its hash.",
            HttpStatusCode.Forbidden => "Access denied. Ask the website administrator to check firewall, proxy and access rules for the printer API.",
            HttpStatusCode.UpgradeRequired => "Agent and website protocol versions are incompatible. Install compatible Ekom.Printer and agent versions.",
            HttpStatusCode.BadRequest => "The website rejected the printer request. Check the station configuration and that the proxy preserves HTTPS and printer protocol headers.",
            HttpStatusCode.TooManyRequests => "The website is limiting requests. Wait before retrying and check the website's rate limits.",
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => "The website redirected the request. Save the final HTTPS website URL, including the correct www hostname or application path. Credentials are not forwarded to redirects.",
            >= HttpStatusCode.InternalServerError => "The website could not process the request. Check website logs, database connectivity and the Ekom.Printer database migration, then retry.",
            not null => "The website rejected the request. Check its printer API configuration and access rules.",
            _ => error.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "Website hostname could not be resolved. Check the URL, DNS and the Windows service account's network access.",
                HttpRequestError.SecureConnectionError => "A secure HTTPS connection could not be established. Check the website certificate, Windows clock and certificate trust. Do not disable certificate validation.",
                _ => "The website could not be reached. Check network connectivity, firewall and proxy access for the Windows service account.",
            },
        };
        return error.StatusCode is { } status ? $"HTTP {(int)status}: {advice}" : advice;
    }

    public const string Timeout = "Website connection timed out after 30 seconds. Check network access from the Windows service account and whether the website is responding, then retry.";
}
