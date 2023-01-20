using Microsoft.Extensions.Configuration;
using System;
using System.Configuration;
using System.Net;
using System.Net.Configuration;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace Ekom.Services
{
    /// <summary>
    /// Handles creation and sending of emails, uses defaults from configuration when possible.
    /// Default assumes a notification email intended for the administrator.
    /// All defaults are overridable.
    /// </summary>
    class MailService : IMailService
    {
        private const int Timeout = 180000;

        private string _sender;
        private string _recipient;

        /// <summary>
        /// ctor
        /// </summary>
        public MailService()
        {
            _recipient = Configuration.Instance.EmailNotifications;

            var config = WebConfigurationManager.OpenWebConfiguration("Web.config");
            var settings = config.GetSectionGroup("system.net/mailSettings") as MailSettingsSectionGroup;

            _sender = settings.Smtp.From;
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
            // We do not catch the error here... let it pass direct to the caller
            using (var smtp = new SmtpClient())
            using (var message = new MailMessage(
                sender ?? _sender,
                recipient ?? _recipient,
                subject,
                body)
            { 
                IsBodyHtml = true 
            })
            {
                smtp.Timeout = Timeout;

                await smtp.SendMailAsync(message).ConfigureAwait(false);
            }
        }
    }
}
