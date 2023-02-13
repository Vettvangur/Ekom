using System.Reflection;

namespace Ekom.Payments;

internal interface IUmbracoService
{
    void PopulatePaymentProviderProperties(
        PaymentSettings settings,
        string ppNodeName,
        object? customProperties,
        IEnumerable<PropertyInfo>? customPropertyList);
}
