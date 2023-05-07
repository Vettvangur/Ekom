using Ekom.Payments.AsynchronousExample;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.AsynchronousExample;

/// <summary>
/// Helper that facilitates easy addition of a prepopulated instance of your payment providers custom settings
/// prior to calling RequestAsync on f.x. Ekom.Payments.AsynchronousExample.Payment
/// </summary>
public static class PaymentSettingsExtensions
{
    public static void SetAsynchronousExampleSettings(
        this PaymentSettings settings, 
        AsynchronousExampleSettings AsynchronousExampleSettings)
    {
        settings.CustomSettings[typeof(AsynchronousExampleSettings)] = AsynchronousExampleSettings;
    }
}
