using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments;
public interface IMailService
{
    string From { get; set; }
    string To { get; set; }

    Task SendAsync(MailMessage mailMessage);
}
