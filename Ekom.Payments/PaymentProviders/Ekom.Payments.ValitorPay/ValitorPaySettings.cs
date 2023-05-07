using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.ValitorPay;

public class ValitorPaySettings : PaymentSettingsBase<ValitorPaySettings>
{
    /// <summary>
    /// Displayed to customer on payment portal page during card verification
    /// </summary>
    public string PaymentPortalDisplayName { get; set; }

    public string ApiUrl { get; set; }

    public string ApiKey { get; set; }

    public string TerminalId { get; set; }

    public string AgreementNumber { get; set; }
}
