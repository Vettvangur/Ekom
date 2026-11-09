namespace Ekom.Exceptions;

public sealed class PriceConflictException : InvalidOperationException
{
    public PriceConflictException() : base("Prices changed since this editor was loaded. Reload before saving.") { }
}
