namespace Ekom.Payments;

/// <summary>
/// Do not attempt to override this property with values from umbraco or appSettings
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class PaymentSettingsIgnoreAttribute : Attribute
{
}
