using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;

namespace Ekom.Models;

public class OrderedVariantGroup
{
    private readonly IVariant variant;
    private readonly StoreInfo storeInfo;

    public int Id { get; set; }
    public Guid Key { get; set; }
    public string Title { get; set; }
    public IEnumerable<OrderedVariant> Variants { get; set; }

    public IReadOnlyDictionary<string, string> Properties;

    /// <summary>
    /// ctor
    /// </summary>
    public OrderedVariantGroup(IVariant variant, IVariantGroup variantGroup, StoreInfo storeInfo, decimal productVat, OrderDynamicRequest? orderDynamic = null)
    {
        this.variant = variant ?? throw new ArgumentNullException(nameof(variant));
        variantGroup = variantGroup ?? throw new ArgumentNullException(nameof(variantGroup));
        storeInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));

        Dictionary<string, string> props
            = variant.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        // Prefer properties from variant group
        variantGroup.Properties
            .ToList()
            .ForEach(x => props[x.Key] = x.Value);

        Properties = new ReadOnlyDictionary<string, string>(props);

        Id = variantGroup.Id;
        Key = variantGroup.Key;
        Title = variantGroup.Title;

        List<OrderedVariant> variants = new List<OrderedVariant>
        {
            new OrderedVariant(variant, storeInfo,productVat,orderDynamic)
        };

        Variants = variants;
    }

    /// <summary>
    /// Json Constructor
    /// </summary>
    public OrderedVariantGroup(JToken variantGroupObject, StoreInfo storeInfo)
    {
        this.storeInfo = storeInfo;

        Properties = new ReadOnlyDictionary<string, string>(
            variantGroupObject[nameof(Properties)].ToObject<Dictionary<string, string>>());

        Id = (int)variantGroupObject[nameof(Id)];
        Key = (Guid)variantGroupObject[nameof(Key)];
        Title = (string)variantGroupObject[nameof(Title)];

        JToken? variants = variantGroupObject[nameof(Variants)];

        List<OrderedVariant> variantsList = new List<OrderedVariant>();

        if (!string.IsNullOrEmpty(variants.ToString()))
        {
            JArray variantsArray = (JArray)variants;

            if (variantsArray != null && variantsArray.Any())
            {

                foreach (JToken variantObject in variantsArray)
                {
                    OrderedVariant variant = new OrderedVariant(variantObject, storeInfo);

                    variantsList.Add(variant);
                }
            }
        }

        if (variantsList.Any())
        {
            Variants = variantsList;
        }
        else
        {
            Variants = Enumerable.Empty<OrderedVariant>();
        }
    }
}
