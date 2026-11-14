namespace Ekom.Payments;

[AttributeUsage(AttributeTargets.Property)]
public sealed class EkomPropertyAttribute : Attribute
{
    public EkomPropertyAttribute(PropertyEditorType propertyEditorType)
    {
        PropertyEditorType = propertyEditorType;
    }

    public PropertyEditorType PropertyEditorType { get; }
}

public enum PropertyEditorType
{
    Store,
    Language
}
