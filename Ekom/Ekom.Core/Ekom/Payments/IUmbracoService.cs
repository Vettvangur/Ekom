using System.Reflection;

namespace Ekom.Payments;

public interface IUmbracoService
{
    void PopulatePaymentProviderProperties(
        PaymentSettings settings,
        string ppNodeName,
        object? customProperties,
        IEnumerable<PropertyInfo>? customPropertyList);
}
