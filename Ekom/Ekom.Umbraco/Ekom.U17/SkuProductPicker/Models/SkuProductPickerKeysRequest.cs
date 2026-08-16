namespace Ekom.Umb.SkuProductPicker.Models;

public sealed class SkuProductPickerKeysRequest
{
    public IReadOnlyList<Guid> Keys { get; init; } = Array.Empty<Guid>();
}
