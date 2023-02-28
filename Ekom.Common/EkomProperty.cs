namespace Ekom;

/// <summary>
/// Indicates a json property containing store specific values
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class EkomPropertyAttribute : Attribute
{
    /// <summary>
    /// Configure as Language or Store based property
    /// </summary>
#pragma warning disable CA1019 // Define accessors for attribute arguments
    public PropertyEditorType PropertyEditorType { get; init; }
#pragma warning restore CA1019 // Define accessors for attribute arguments

    public EkomPropertyAttribute(PropertyEditorType propertyEditorType)
    {
        PropertyEditorType = propertyEditorType;
    }
}
