namespace Ekom;

/// <summary>
/// Indicates a json property containing store specific values
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class EkomPropertyAttribute : Attribute
{
}
