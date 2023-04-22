using Ekom.Payments.ValitorPay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.ValitorPay;

public static class PaymentSettingsExtensions
{
    public static void SetValitorPaySettings(
        this PaymentSettings settings, 
        ValitorPaySettings valitorPaySettings)
    {
        settings.CustomSettings[typeof(ValitorPaySettings)] = valitorPaySettings;
    }
}
