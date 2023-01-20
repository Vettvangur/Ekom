using Ekom.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Configuration;

namespace Ekom.AspNetCore.Services
{
    /// <summary>
    /// Handles creation and sending of emails, uses defaults from configuration when possible.
    /// Default assumes a notification email intended for the administrator.
    /// All defaults are overridable.
    /// </summary>
    class MailService : IMailService
    {
        private const int Timeout = 180000;
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;
        private readonly bool _ssl;

        private string _sender;
        private string _recipient;

        /// <summary>
        /// ctor
        /// </summary>
        public MailService(IConfiguration config)
        {
            _recipient = Configuration.Instance.EmailNotifications;

            //MailServer - Represents the SMTP Server
            _host = config["Smtp:Host"];
            //Port- Represents the port number
            _port = 25; 

            if (int.TryParse(config["Smtp:Port"], out int port))
            {
                _port = port;
            }

            //MailAuthUser and MailAuthPass - Used for Authentication for sending email
            _user = config["Smtp:UserName"];
            _pass = config["Smtp:Password"];

            _ssl = false;

            if (bool.TryParse(config["Smtp:EnableSsl"], out bool ssl))
            {
                _ssl = ssl;
            }

            _sender = config["Smtp:FromAddress"];
        }

        /// <summary>
        /// Send email message
        /// </summary>
        public async virtual Task SendAsync(
            string subject,
            string body,
            string recipient = null,
            string sender = null)
        {
            if (string.IsNullOrEmpty(_host))
            {
                throw new Exception("Smtp:Host is not configured");
            }
            
            // We do not catch the error here... let it pass direct to the caller
            using (var smtp = new SmtpClient(_host, _port))
            using (var message = new MailMessage(
                sender ?? _sender,
                recipient ?? _recipient,
                subject,
                body)
            { IsBodyHtml = true })
            {
                if (_user.Length > 0 && _pass.Length > 0)
                {
                    smtp.Timeout = Timeout;
                    smtp.UseDefaultCredentials = false;
                    smtp.Credentials = new NetworkCredential(_user, _pass);
                    smtp.EnableSsl = _ssl;
                }

                await smtp.SendMailAsync(message).ConfigureAwait(false);
            }
        }
    }
}
