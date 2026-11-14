using System.Reflection;

namespace Ekom.Payments;

/// <summary>
/// Base configuration for PaymentProviders. 
/// </summary>
public class PaymentSettingsBase<T>
{
    /// <summary>
    /// We iterate over this collection and allow overriding via code/umbraco/appsettings.
    /// The collection can be replaced as well to customize.
    /// </summary>
    public static IReadOnlyCollection<PropertyInfo> Properties
        = typeof(T)
                .GetProperties()
                .Where(prop => prop.CustomAttributes
                    .All(x => x.AttributeType != typeof(PaymentSettingsIgnoreAttribute)))
                .ToList()
                .AsReadOnly()
        ;
}
