using Ekom.Cache;
using Ekom.Models;
using Ekom.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
namespace Ekom;

/// <summary>
/// Controls configuration of Ekom
/// </summary>
public class Configuration
{
    readonly IConfiguration _configuration;
    private List<CharCollection> _charCollectionsCache;
    public Configuration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public static IServiceProvider Resolver { get; set; }

    /// <summary>
    /// This IServiceProvider is a root scope provider,
    /// that means any services and their dependencies can only be singletons/transients.
    /// If any services resolved with this root scope provider require scoped lifetime services,
    /// get yourself an HttpContext (with an accessor) and RequestServices.GetService them.
    /// </summary>
    public static Configuration Instance => Resolver.GetService<Configuration>();

    internal const string Cookie_UmbracoDomain = "EkomUmbracoDomain";
    public static string ImportAliasIdentifier => "ekmIdentifier";
    public static string EkmRequestKey = "umbrtmche-ekmRequest";

    /// <summary>
    /// Ekom:PerStoreStock
    /// Controls which stock cache to will be used. Per store or per Product/Variant.
    /// </summary>
    public virtual bool PerStoreStock
    {
        get
        {
            string? value = _configuration["Ekom:PerStoreStock"];

            return value.ConvertToBool();
        }
    }

    /// <summary>
    /// Ekom:ExamineSearchIndex
    /// Overrides the default of ExternalSearcher
    /// </summary>
    public virtual string ExamineSearchIndex
    {
        get
        {
            string? value = _configuration["Ekom:ExamineSearchIndex"];

            return value ?? "ExternalIndex";
        }
    }

    /// <summary>
    /// Umbraco:CMS:RequestHandler:CharCollection
    /// Gets the Umbraco CharCollection
    /// </summary>
    public virtual List<CharCollection> CharCollections
    {
        get
        {
            if (_charCollectionsCache != null)
            {
                return _charCollectionsCache;
            }

            _charCollectionsCache = _configuration.GetSection("Umbraco:CMS:RequestHandler:UserDefinedCharCollection").Get<List<CharCollection>>() ?? new List<CharCollection>();

            return _charCollectionsCache;
        }
    }

    public class CharCollection
    {
        public string Char { get; set; }
        public string Replacement { get; set; }
    }

    /// <summary>
    /// Ekom:ShareBasket
    /// This will allow baskets to be shared between stores but we aware that it requires the same currencies to be available cross stores or it will break down.
    /// </summary>
    public virtual bool ShareBasketBetweenStores
    {
        get
        {
            string? value = _configuration["Ekom:ShareBasket"];

            return value.ConvertToBool();
        }
    }

    public HeadlessConfig? HeadlessConfig()
    {
        IConfigurationSection headlessSection = _configuration.GetSection("Ekom:Headless");

        if (!headlessSection.Exists())
        {
            return null;
        }

        var headlessConfig = headlessSection.Get<HeadlessConfig>();

        if (headlessConfig == null || !headlessConfig.ReValidateApis.Any())
        {
            return null;
        }

        return headlessConfig;
    }

    /// <summary>
    /// Ekom:AbsoluteUrls
    /// This will set all backoffice urls to absolute to work better with multi site setup
    /// </summary>
    public virtual bool AbsoluteUrls
    {
        get
        {
            string? value = _configuration["Ekom:AbsoluteUrls"];

            return value.ConvertToBool();
        }
    }

    public virtual OrderBy DefaultProductOrderBy
    {
        get
        {
            string? value = _configuration["Ekom:DefaultProductOrderBy"];
            if (string.IsNullOrEmpty(value))
            {
                return OrderBy.DateDesc;
            }
            if (Enum.TryParse<OrderBy>(value, out OrderBy orderBy))
            {
                return orderBy;
            }
            return OrderBy.DateDesc;
        }
    }

    /// <summary>
    /// Ekom:BasketCookieLifetime
    /// Set how many days the order cookie should live, Default 1 day
    /// </summary>
    public virtual double BasketCookieLifetime
    {
        get
        {
            string? value = _configuration["Ekom:BasketCookieLifetime"];

            double _value = 1;

            if (double.TryParse(value, out double __value))
            {
                _value = __value;
            }

            return _value;
        }
    }
    /// <summary>
    /// Ekom:GlobalCatalog
    /// If true if the catalog item is not found in current store we try to find it in all other stores.
    /// </summary>
    public virtual bool GlobalCatalog
    {
        get
        {
            return _configuration["Ekom:GlobalCatalog"]?.ConvertToBool() ?? false;
        }
    }

    /// <summary>
    /// Ekom:CustomImage
    /// Overrides the default of images
    /// </summary>
    public virtual string CustomImage
    {
        get
        {
            string? value = _configuration["Ekom:CustomImage"];

            return value ?? "images";
        }
    }

    /// <summary>
    /// Ekom:SectionAccessRules Access Rules
    /// Section Group alias acess rules, comma sepereated
    /// </summary>
    public virtual string[] SectionAccessRules
    {
        get
        {
            string? value = _configuration["Ekom:SectionAccessRules"];

            if (!string.IsNullOrEmpty(value))
            {
                return value.Split(',');
            }

            return [];
        }
    }

    /// <summary>
    /// Ekom:CategoryRootLevel
    /// Umbraco level value. Minimum level value for categories in umbraco hierarchy.
    /// </summary>
    public virtual int CategoryRootLevel
    {
        get
        {
            return int.Parse(_configuration["Ekom:CategoryRootLevel"] ?? "3");
        }
    }

    /// <summary>
    /// Time we give the user to complete checkout.
    /// Give value in minutes when overriding default
    /// </summary>
    public virtual TimeSpan ReservationTimeout
        => TimeSpan.FromMinutes(double.Parse(_configuration["Ekom:ReservationTimeout"] ?? "30"));

    /// <summary>
    /// Should Ekom create a ekmCustomerData table and use it to store customer + order data 
    /// submitted to the checkout controller?
    /// </summary>
    public virtual bool StoreCustomerData
        => _configuration["Ekom:CustomerData"]?.ConvertToBool() ?? false;

    /// <summary>
    /// </summary>
    public virtual Rounding VatCalculationRounding
    {
        get
        {
            string? configVal = _configuration["Ekom:VatCalcRounding"];

            if (!Enum.TryParse(configVal, out Rounding preferredRounding))
            {
                // Default
                preferredRounding = Rounding.AwayFromZero;
            }

            return preferredRounding;
        }
    }

    public virtual VatIncludedPerUnitPolicy VatIncludedPerUnitPolicy
    {
        get
        {
            string? configVal = _configuration["Ekom:VatIncludedPerUnitPolicy"];

            if (!Enum.TryParse(configVal, out VatIncludedPerUnitPolicy preferredRounding))
            {
                // Default
                preferredRounding = VatIncludedPerUnitPolicy.PreserveStickerGross;
            }

            return preferredRounding;
        }
    }

    public virtual VatRoundingScope VatRoundingScope
    {
        get
        {
            string? configVal = _configuration["Ekom:VatRoundingScope"];

            if (!Enum.TryParse(configVal, out VatRoundingScope preferredRoundingScope))
            {
                // Default
                preferredRoundingScope = VatRoundingScope.PerUnit;
            }

            return preferredRoundingScope;
        }
    }

    /// <summary>
    /// Ekom:UserBasket
    /// Single basket per user, not saved in session or cookie, only on the member under "orderId".
    /// </summary>
    public virtual bool UserBasket
    {
        get
        {
            string? value = _configuration["Ekom:UserBasket"];

            return value.ConvertToBool();
        }
    }

    /// <summary>
    /// Used by MailService, defaults to umbracoSettings.config configured email
    /// </summary>
    public virtual string? EmailNotifications
        => _configuration["Ekom:EmailNotifications"];

    /// <summary>
    /// </summary>
    public virtual bool DisableStock
        => _configuration["Ekom:DisableStock"]?.ConvertToBool() ?? false;

    /// <summary>
    /// Lists in initialization order all caches and the document type alias of
    /// the object they cache.
    /// This object is lazy initialized to make sure that all types have been registered with IoC container
    /// before we attempt to resolve.
    /// 
    /// The order in this list is important as addition and removal from caches triggers updates on succeeding caches.
    /// </summary>
    internal virtual Lazy<List<ICache>> CacheList { get; } = new Lazy<List<ICache>>(()
        => new List<ICache>
        {
            { Resolver.GetService<IStoreDomainCache>() },
            { Resolver.GetService<IBaseCache<IStore>>() },
            { Resolver.GetService<IPerStoreCache<ICategory>>() },
            { Resolver.GetService<IPerStoreCache<IProductDiscount>>() },
            { Resolver.GetService<IPerStoreCache<IProduct>>() },
            { Resolver.GetService<IPerStoreCache<IVariant>>() },
            { Resolver.GetService<IPerStoreCache<IVariantGroup>>() },
            { Resolver.GetService<IBaseCache<IZone>>() },
            { Resolver.GetService<IPerStoreCache<IPaymentProvider>>() },
            { Resolver.GetService<IPerStoreCache<IShippingProvider>>() },
            { Resolver.GetService<IPerStoreCache<IDiscount>>() },
        }
    );

    /// <summary> 
    /// Returns all <see cref="ICache"/> in the sequence succeeding the given cache 
    /// </summary> 
    internal IEnumerable<ICache> Succeeding(ICache cache)
    {
        int indexOf = CacheList.Value.FindIndex(x => x == cache);

        return CacheList.Value.Skip(indexOf + 1);
    }

    internal const string DiscountStockTableName = "EkomDiscountStock";

    internal static readonly TimeSpan orderInfoCacheTime = TimeSpan.FromDays(1);

    /// <summary>
    /// Useful for Azure app services which sometimes do not include the correct currency symbol
    /// </summary>
    public static CultureInfo IsCultureInfo
    {
        get
        {
            CultureInfo ci = new CultureInfo("is-IS");
            ci.NumberFormat.CurrencySymbol = "kr";
            return ci;
        }
    }
}
