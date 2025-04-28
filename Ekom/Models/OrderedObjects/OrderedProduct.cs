using Ekom.API;
using Ekom.Services;
using Ekom.Utilities;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Xml.Serialization;

namespace Ekom.Models;

public class OrderedProduct
{

    public OrderedProduct()
    {
    }

    public int Id
    {
        get
        {
            if (Properties.ContainsKey("id"))
            {
                return Convert.ToInt32(Properties.GetPropertyValue("id"));
            }

            return 0;
        }
    }

    public Guid Key
    {
        get
        {
            if (Properties.ContainsKey("__Key"))
            {
                string key = Properties.GetPropertyValue("__Key");

                if (!Guid.TryParse(key, out Guid _key))
                {
                    throw new Exception("No key present for product.");
                }

                return _key;
            }

            // Backword Compatability
            return Guid.Empty;

        }
    }

    public string SKU
    {
        get
        {
            return Properties.GetPropertyValue("sku");
        }
    }
    public IDiscount ProductDiscount { get; }

    public string Title
    {
        get
        {
            return Properties.GetPropertyValue("title", StoreInfo.Alias, fallback: true);
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public string Path => Properties.GetPropertyValue("__Path");

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    //TODO Store default setup!
    public bool Backorder => Properties.GetPropertyValue("enableBackorder", StoreInfo.Alias).IsBoolean();

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public DateTime CreateDate
    {
        get
        {
            return UtilityService.ConvertToDatetime(Properties.GetPropertyValue("createDate"));
        }
    }
    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    public DateTime UpdateDate
    {
        get
        {
            return UtilityService.ConvertToDatetime(Properties.GetPropertyValue("updateDate"));
        }
    }

    public IPrice Price
    {
        get
        {
            return Prices.FirstOrDefault(x => x.Currency.CurrencyValue == StoreInfo.Currency.CurrencyValue);
        }
    }

    public List<IPrice> Prices { get; }

    public decimal Vat
    {
        get
        {
            if (Properties.HasPropertyValue("vat", StoreInfo.Alias))
            {
                string value = Properties.GetPropertyValue("vat", StoreInfo.Alias);

                if (!string.IsNullOrEmpty(value) && decimal.TryParse(value, out decimal _val))
                {
                    return _val / 100;
                }
            }

            return StoreInfo.Vat;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    [Newtonsoft.Json.JsonIgnore]
    [XmlIgnore]
    private StoreInfo StoreInfo { get; }

    public IReadOnlyDictionary<string, string> Properties;

    public IEnumerable<OrderedVariantGroup> VariantGroups { get; set; }


    // <summary>
    // Product images
    // </summary>
    public virtual IEnumerable<Image> Images
    {
        get
        {
            //var primaryVariantGroup = PrimaryVariantGroup;

            //if (primaryVariantGroup != null)
            //{
            //    var imageNodes = primaryVariantGroup.Images.ToList();

            //    if (!imageNodes.Any() && primaryVariantGroup.Variants.Any())
            //    {
            //        imageNodes = primaryVariantGroup.Variants.FirstOrDefault()?.Images.ToList();
            //    }

            //    if (imageNodes.Any())
            //    {
            //        return imageNodes;
            //    }
            //}

            string _images = Properties.GetPropertyValue(Configuration.Instance.CustomImage);

            return _images.GetImages();
        }
    }

    public virtual int ParentId
    {
        get
        {
            if (int.TryParse(Properties.GetPropertyValue("parentID"), out int _parentId))
            {
                return _parentId;
            }

            return 0;
        }
    }

    public virtual string Url
    {
        get
        {
            IProduct? productNode = Catalog.Instance.GetProduct(Key, StoreInfo.Alias);

            if (productNode != null)
            {
                return productNode.Url;
            }

            return "";
        }
    }

    public IEnumerable<IProduct> RelatedProducts(int count = 4)
    {
        List<IProduct> relatedProducts = new List<IProduct>();

        if (Properties.HasPropertyValue("relatedProducts"))
        {
            string val = Properties.GetPropertyValue("relatedProducts");

            if (!string.IsNullOrEmpty(val))
            {
                bool relatedProductIds = UtilityService.ConvertUdisToGuids(val, out IEnumerable<Guid> guids);

                foreach (Guid id in guids.Where(x => x != Key).Take(count))
                {
                    IProduct? product = Catalog.Instance.GetProduct(id, StoreInfo.Alias);

                    if (product != null && product.Key != id)
                    {
                        relatedProducts.Add(product);
                    }
                }
            }
        }

        if (!relatedProducts.Any())
        {
            ICategory? category = Catalog.Instance.GetCategory(ParentId, StoreInfo.Alias);

            if (category != null)
            {
                relatedProducts = category.ProductsRecursive().Products.Where(x => x.Id != Id).Take(count).ToList();
            }
        }

        return relatedProducts;
    }

    /// <summary>
    /// ctor
    /// </summary>
    public OrderedProduct(IProduct product, IVariant variant, StoreInfo storeInfo, OrderDynamicRequest? orderDynamic = null)
    {
        product = product ?? throw new ArgumentNullException(nameof(product));
        StoreInfo = storeInfo ?? throw new ArgumentNullException(nameof(storeInfo));

        Dictionary<string, string> productDictionary = product.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (orderDynamic != null && !string.IsNullOrEmpty(orderDynamic.Title))
        {
            productDictionary["title"] = orderDynamic.Title;
        }
        if (orderDynamic != null && !string.IsNullOrEmpty(orderDynamic.SKU))
        {
            productDictionary["sku"] = orderDynamic.SKU;
        }
        if (orderDynamic != null && !string.IsNullOrEmpty(orderDynamic.Type))
        {
            productDictionary["dynamicType"] = orderDynamic.Type;
        }

        Properties = new ReadOnlyDictionary<string, string>(
           productDictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));

        if (orderDynamic != null && orderDynamic.Prices != null && orderDynamic.Prices.Any())
        {
            Prices = orderDynamic.Prices;
        }
        else
        {
            Prices = product.Prices.ToList();
        }

        IDiscount productDiscount = product.ProductDiscount(Price.Value.ToString());

        ProductDiscount = productDiscount != null ? new OrderedDiscount(productDiscount) : null;

        if (variant != null)
        {
            List<OrderedVariantGroup> variantGroups = new List<OrderedVariantGroup>();

            IVariantGroup variantGroup = variant.VariantGroup;

            variantGroups.Add(new OrderedVariantGroup(variant, variantGroup, storeInfo, Vat, orderDynamic));

            VariantGroups = variantGroups;
        }
        else
        {
            VariantGroups = Enumerable.Empty<OrderedVariantGroup>();
        }
    }

    /// <summary>
    /// ctor
    /// </summary>
    public OrderedProduct(string productJson, StoreInfo storeInfo)
    {
        StoreInfo = storeInfo;

        JObject productPropertiesObject = JObject.Parse(productJson);
        ProductDiscount = productPropertiesObject[nameof(ProductDiscount)]?
            .ToObject<IDiscount>(EkomJsonDotNet.serializer);

        Properties = new ReadOnlyDictionary<string, string>(
            productPropertiesObject[nameof(Properties)].ToObject<Dictionary<string, string>>());

        JToken? pricesObj = productPropertiesObject[nameof(Prices)];

        JToken? priceObj = productPropertiesObject[nameof(Price)];

        try
        {
            if (pricesObj != null && !string.IsNullOrEmpty(pricesObj.ToString()))
            {

                Prices = pricesObj.ToString().GetPriceValuesConstructed(Vat, storeInfo.VatIncludedInPrice, storeInfo.Currency);
            }
            else
            {
                try
                {
                    Prices = new List<IPrice>()
                    {
                        priceObj.ToObject<Price>(EkomJsonDotNet.serializer)
                    };
                }
                catch
                {
                    Prices = new List<IPrice>()
                    {
                        new Price(priceObj, storeInfo.Currency, storeInfo.Vat, storeInfo.VatIncludedInPrice)
                    };
                }
            }

        }
        catch (Exception)
        {
            //logger.Error<OrderedProduct>(ex,"Failed to construct price. ID: " + Id + " Price Object: " + (priceObj != null ? priceObj.ToString() : "Null") + " Prices Object: " + (pricesObj != null ? pricesObj.ToString() : "Null"));
        }

        // Add Variant Group

        JToken? variantGroups = productPropertiesObject[nameof(VariantGroups)];

        List<OrderedVariantGroup> variantsGroupList = new List<OrderedVariantGroup>();

        if (variantGroups != null && !string.IsNullOrEmpty(variantGroups.ToString()))
        {
            JArray variantGroupsArray = (JArray)variantGroups;

            if (variantGroupsArray != null && variantGroupsArray.Any())
            {
                foreach (JToken variantGroupObject in variantGroupsArray)
                {
                    OrderedVariantGroup variantGroup = new OrderedVariantGroup(variantGroupObject, storeInfo);

                    variantsGroupList.Add(variantGroup);
                }
            }
        }

        if (variantsGroupList.Any())
        {
            VariantGroups = variantsGroupList;
        }
        else
        {
            VariantGroups = Enumerable.Empty<OrderedVariantGroup>();
        }
    }
}
