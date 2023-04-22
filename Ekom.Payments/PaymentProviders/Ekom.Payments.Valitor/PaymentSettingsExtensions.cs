using Ekom.Payments.Valitor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.Valitor;

public static class PaymentSettingsExtensions
{
    public static void SetValitorSettings(
        this PaymentSettings settings, 
        ValitorSettings valitorSettings)
    {
        settings.CustomSettings[typeof(ValitorSettings)] = valitorSettings;
    }
}
