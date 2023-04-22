using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.ValitorPay;

public class ValitorPaySettings : PaymentSettingsBase<ValitorPaySettings>
{
    public string ApiUrl { get; set; }

    public string ApiKey { get; set; }

    public string TerminalId { get; set; }

    public string AgreementNumber { get; set; }

    /// <summary>
    /// 16 digit payment card number
    /// </summary>
    public string CardNumber { get; set; }

    public int ExpirationMonth { get; set; }
    
    public int ExpirationYear { get; set; }
    
    /// <summary>
    /// Card Verification Value. Triple digit number on the back of the card.
    /// </summary>
    public string CVV { get; set; }
    
    public string VirtualCardNumber { get; set; }
}
