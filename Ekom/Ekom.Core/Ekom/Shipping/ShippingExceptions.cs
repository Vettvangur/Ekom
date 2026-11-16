namespace Ekom.Shipping;

public class ShippingException : Exception
{
    public ShippingException(string message) : base(message)
    {
    }

    public ShippingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class ShippingConfigurationException : ShippingException
{
    public ShippingConfigurationException(string message) : base(message)
    {
    }
}

public sealed class ShippingProviderException : ShippingException
{
    public ShippingProviderException(string carrierAlias, string message)
        : base(message)
    {
        CarrierAlias = carrierAlias;
    }

    public ShippingProviderException(string carrierAlias, string message, Exception innerException)
        : base(message, innerException)
    {
        CarrierAlias = carrierAlias;
    }

    public string CarrierAlias { get; }
}

public sealed class InvalidShippingSelectionException : ShippingException
{
    public InvalidShippingSelectionException(string message) : base(message)
    {
    }
}

public sealed class ShipmentOutcomeUnknownException : ShippingException
{
    public ShipmentOutcomeUnknownException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
