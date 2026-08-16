namespace Ekom.Umb.SkuProductPicker.Models;

public sealed class SkuProductPickerSkusRequest
{
    public IReadOnlyList<string> Skus { get; init; } = Array.Empty<string>();
}
