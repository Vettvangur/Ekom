using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace Ekom.Payments;

/// <summary>
/// Handles creation and sending of emails, uses defaults from configuration when possible.
/// Default assumes a notification email intended for the administrator.
/// All defaults are overridable.
/// </summary>
class MailService : IMailService
{
    readonly ILogger _logger;

    public string From { get; set; }

    public string To { get; set; }

    readonly string _host;
    readonly int _port;
    readonly string _username;
    readonly string _password;

    public MailService(
        ILogger<MailService> logger,
        string host,
        string port,
        string username,
        string password,
        string from,
        string to)
    {
        _logger = logger;

        if (string.IsNullOrEmpty(host)
        && string.IsNullOrEmpty(port)
        && string.IsNullOrEmpty(username)
        && string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("No SMTP configuration found, email will not be sent");
            return;
        }

        _host = host;
        _port = int.Parse(port);
        _username = username;
        _password = password;

        From = from;
        To = to;
    }

    /// <summary>
    /// Send email message
    /// </summary>
    public async virtual Task SendAsync(MailMessage mailMessage)
    {
        mailMessage.From = mailMessage.From ?? new MailAddress(From);

        if (!mailMessage.To.Any())
        {
            mailMessage.To.Add(new MailAddress(To));
        }
        mailMessage.IsBodyHtml = true;

        _logger.LogInformation(
            $"Sending mail message from {mailMessage.From?.Address ?? From} to " +
            $"{mailMessage.To.FirstOrDefault()?.Address ?? To} with subject {mailMessage.Subject}");

        // We do not catch the error here... let it pass direct to the caller
        using (var smtp = new SmtpClient(_host, _port))
        using (mailMessage)
        {
            smtp.Credentials = new NetworkCredential(_username, _password);
            smtp.EnableSsl = true;
            await smtp.SendMailAsync(mailMessage).ConfigureAwait(false);
        }
    }
}
