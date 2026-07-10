using Ekom.Models;
using Ekom.Models.Umbraco;

namespace Ekom.Umb.VariantApp.Models;

public sealed class VariantManagerProduct
{
    public int Id { get; set; }
    public Guid Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public int VariantCount { get; set; }
    public IReadOnlyList<UmbracoLanguage> Languages { get; set; } = Array.Empty<UmbracoLanguage>();
    public IReadOnlyList<VariantManagerStore> Stores { get; set; } = Array.Empty<VariantManagerStore>();
    public IReadOnlyList<VariantManagerCustomFieldDefinition> VariantGroupFields { get; set; } = Array.Empty<VariantManagerCustomFieldDefinition>();
    public IReadOnlyList<VariantManagerCustomFieldDefinition> VariantFields { get; set; } = Array.Empty<VariantManagerCustomFieldDefinition>();
    public IReadOnlyList<VariantManagerGroup> Groups { get; set; } = Array.Empty<VariantManagerGroup>();
}

public sealed class VariantManagerCount
{
    public int Count { get; set; }
}

public sealed class VariantManagerStore
{
    public string Alias { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<CurrencyModel> Currencies { get; set; } = Array.Empty<CurrencyModel>();
}

public class VariantManagerCustomFieldDefinition
{
    public string Alias { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
}

public sealed class VariantManagerCustomField : VariantManagerCustomFieldDefinition
{
    public string Value { get; set; } = string.Empty;
}

public sealed class VariantManagerGroup
{
    public int Id { get; set; }
    public Guid Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IDictionary<string, string> TitleValues { get; set; } = new Dictionary<string, string>();
    public string Color { get; set; } = string.Empty;
    public string Images { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Changed { get; set; }
    public bool Published { get; set; }
    public IReadOnlyList<VariantManagerCustomField> CustomFields { get; set; } = Array.Empty<VariantManagerCustomField>();
    public IReadOnlyList<VariantManagerVariant> Variants { get; set; } = Array.Empty<VariantManagerVariant>();
}

public sealed class VariantManagerVariant
{
    public int Id { get; set; }
    public Guid Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IDictionary<string, string> TitleValues { get; set; } = new Dictionary<string, string>();
    public string Sku { get; set; } = string.Empty;
    public string Images { get; set; } = string.Empty;
    public CurrencyPriceRoot PriceValues { get; set; } = new();
    public IReadOnlyList<StockRequest> StockValues { get; set; } = Array.Empty<StockRequest>();
    public IReadOnlyList<VariantManagerCustomField> CustomFields { get; set; } = Array.Empty<VariantManagerCustomField>();
    public int SortOrder { get; set; }
    public bool Changed { get; set; }
    public bool Published { get; set; }
}

public sealed class VariantManagerGroupRequest
{
    public string ProductId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Images { get; set; } = string.Empty;
    public bool Publish { get; set; }
}

public sealed class VariantManagerVariantRequest
{
    public string GroupId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Images { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string Stock { get; set; } = string.Empty;
    public bool Publish { get; set; }
}

public sealed class VariantManagerSaveRequest
{
    public string ProductId { get; set; } = string.Empty;
    public bool Publish { get; set; }
    public IReadOnlyList<VariantManagerGroup> Groups { get; set; } = Array.Empty<VariantManagerGroup>();
}

public sealed class VariantManagerGroupSaveRequest
{
    public string ProductId { get; set; } = string.Empty;
    public bool Publish { get; set; }
    public VariantManagerGroup Group { get; set; } = new();
}

public sealed class VariantManagerVariantSaveRequest
{
    public string GroupId { get; set; } = string.Empty;
    public bool Publish { get; set; }
    public VariantManagerVariant Variant { get; set; } = new();
}
