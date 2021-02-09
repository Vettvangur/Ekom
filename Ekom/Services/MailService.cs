using System.Configuration;
using System.Net;
using System.Net.Configuration;
using System.Net.Mail;
using System.Threading.Tasks;
using Umbraco.Core.Configuration.UmbracoSettings;

namespace Ekom.Services
{
    /// <summary>
    /// Handles creation and sending of emails, uses defaults from configuration when possible.
    /// Default assumes a notification email intended for the administrator.
    /// All defaults are overridable.
    /// </summary>
    class MailService
    {
        private const int Timeout = 180000;
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;
        private readonly bool _ssl;

        /// <summary>
        /// Defaults to "no-reply@umbraco.netpayment"
        /// </summary>
        public string Sender { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Recipient { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string Subject { get; set; }
        /// <summary>
        /// Email message body
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="contentSectionConfig">/config/umbracoSettings.config</param>
        public MailService(IContentSection contentSectionConfig)
        {
            var smtpSection = (SmtpSection)ConfigurationManager.GetSection("system.net/mailSettings/smtp");
            Recipient = contentSectionConfig.NotificationEmailAddress;

            //MailServer - Represents the SMTP Server
            _host = smtpSection.Network.Host;
            //Port- Represents the port number
            _port = smtpSection.Network.Port;
            //MailAuthUser and MailAuthPass - Used for Authentication for sending email
            _user = smtpSection.Network.UserName;
            _pass = smtpSection.Network.Password;
            _ssl = smtpSection.Network.EnableSsl;

            Sender = smtpSection.From;
        }

        /// <summary>
        /// Send email message
        /// </summary>
        public async virtual Task SendAsync()
        {
            // We do not catch the error here... let it pass direct to the caller
            using (var smtp = new SmtpClient(_host, _port))
            using (var message = new MailMessage(Sender, Recipient, Subject, Body) { IsBodyHtml = true })
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
