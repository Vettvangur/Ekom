using Ekom.Payments.Exceptions;
using Ekom.Payments.Helpers;
#if !UMBRACO_19
using Examine;
#endif
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using Umbraco.Cms.Core.Models.PublishedContent;
#if UMBRACO_19
using Umbraco.Cms.Core.Services.Navigation;
#endif
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;
using static Umbraco.Cms.Core.Constants;

namespace Ekom.Payments.Umb;

class UmbracoService : IUmbracoService
{
    readonly ILogger<UmbracoService> _logger;
    readonly IConfiguration _configuration;
#if UMBRACO_19
    readonly IDocumentNavigationQueryService _documentNavigationQueryService;
#else
    readonly IExamineManager _examineManager;
#endif
    readonly IUmbracoHelperAccessor _umbracoHelperAccessor;
    readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;

    static MethodInfo publishedContentValueMethod = typeof(FriendlyPublishedContentExtensions)
            .GetMethods()
            .First(x => x.Name == "Value" && x.IsGenericMethodDefinition)
        ;

    public UmbracoService(
#if UMBRACO_19
        IDocumentNavigationQueryService documentNavigationQueryService,
#else
        IExamineManager examineManager,
#endif
        IUmbracoHelperAccessor umbracoHelperAccessor,
        IConfiguration configuration,
        ILogger<UmbracoService> logger,
        IHttpContextAccessor httpContextAccessor, IUmbracoContextFactory umbracoContextFactory)
    {
#if UMBRACO_19
        _documentNavigationQueryService = documentNavigationQueryService;
#else
        _examineManager = examineManager;
#endif
        _umbracoHelperAccessor = umbracoHelperAccessor;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _umbracoContextFactory = umbracoContextFactory;
    }

#if UMBRACO_19
    /// <summary>
    /// Umbraco 19 removed the Examine indexes, so the container is located
    /// through the document navigation structure and the published cache.
    /// </summary>
    private IPublishedContent? GetProviderContainer(UmbracoHelper umbracoHelper)
    {
        var alias = PaymentsConfiguration.ContainerDocumentTypeAlias;
        var keys = new List<Guid>();

        if (_documentNavigationQueryService.TryGetRootKeysOfType(alias, out var rootKeysOfType))
        {
            keys.AddRange(rootKeysOfType);
        }

        if (_documentNavigationQueryService.TryGetRootKeys(out var rootKeys))
        {
            foreach (var rootKey in rootKeys)
            {
                if (_documentNavigationQueryService.TryGetDescendantsKeysOfType(rootKey, alias, out var descendantKeys))
                {
                    keys.AddRange(descendantKeys);
                }
            }
        }

        // The navigation structure includes unpublished nodes, the published cache does not.
        var container = keys
            .Select(key => umbracoHelper.Content(key))
            .FirstOrDefault(content => content != null);

        return container
            ?? throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider container node : " + alias);
    }
#else
    private IPublishedContent? GetProviderContainer(UmbracoHelper umbracoHelper)
    {
        if (!_examineManager.TryGetIndex(UmbracoIndexes.ExternalIndexName, out var index))
        {
            throw new InvalidOperationException("Could not access the Examine searcher");
        }

        var searchResults = index
            .Searcher
            .CreateQuery("content")
            .NodeTypeAlias(PaymentsConfiguration.ContainerDocumentTypeAlias)
            .Execute();

        if (searchResults?.Any() != true)
        {
            throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider container node : " + PaymentsConfiguration.ContainerDocumentTypeAlias);
        }

        return umbracoHelper.Content(searchResults.First().Id);
    }
#endif

    private IPublishedContent GetPPNode(PaymentSettings paymentSettings, string ppNodeName)
    {
        IPublishedContent ppNode;
        if (paymentSettings.PaymentProviderKey != default)
        {
            ppNode = GetPPNode(paymentSettings.PaymentProviderKey);
        }
        else if (!string.IsNullOrEmpty(paymentSettings.PaymentProviderName))
        {
            ppNode = GetPPNode(paymentSettings.PaymentProviderName);
        }
        else
        {
            ppNode = GetPPNode(ppNodeName);
        }

        return ppNode ?? throw new PaymentProviderNotFoundException(nameof(ppNode));
    }

    /// <summary>
    /// Get umbraco content by node name
    /// </summary>
    /// <param name="ppNodeName">Payment Provider Node Name</param>
    private IPublishedContent GetPPNode(string ppNodeName)
    {
        if (!_umbracoHelperAccessor.TryGetUmbracoHelper(out var umbracoHelper))
        {
            throw new InvalidOperationException("Could not access the Umbraco helper");
        }

        var ppContainer = GetProviderContainer(umbracoHelper);

        if (ppContainer == null) throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider container node: " + ppNodeName);

        var visibleChildren = ppContainer.Children().Where(x => x.IsVisible());

        return visibleChildren.FirstOrDefault(x =>
                   x.Name.Equals(ppNodeName, StringComparison.InvariantCultureIgnoreCase))
               ?? visibleChildren.First(x =>
                   x.ContentType.Alias.Equals(ppNodeName, StringComparison.InvariantCultureIgnoreCase));
    }

    private IPublishedContent GetPPNode(Guid key)
    {
        using var umbracoContextReference = _umbracoContextFactory.EnsureUmbracoContext();
        
        var ppNode = umbracoContextReference.UmbracoContext.Content.GetById(false, key);

        if (ppNode == null) throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider node: " + key);

        return ppNode;
    }

    /// <summary>
    /// Populates the payment settings and per payment provider custom settings objects. <br />
    /// The order of precedence is: <br />
    /// assigned object property, umbraco node property value, appsettings configuration value
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="ppNodeName"></param>
    /// <param name="customProperties"></param>
    /// <param name="customPropertyList"></param>
    public void PopulatePaymentProviderProperties(
        PaymentSettings settings,
        string ppNodeName,
        object? customProperties,
        IEnumerable<PropertyInfo>? customPropertyList)
    {
        _logger.LogDebug("Populating properties for {PaymentProvider}", ppNodeName);

        IPublishedContent ppNode = GetPPNode(settings, ppNodeName);

        settings.PaymentProviderName = ppNode.Name;
        settings.PaymentProviderKey = ppNode.Key;

        if (string.IsNullOrEmpty(settings.OrderName))
        {
            var sb = new StringBuilder();

            foreach (var order in settings.Orders)
            {
                sb.Append(order.Title + " ");
            }

            var orderName = sb.ToString().TrimEnd(' ');

            settings.OrderName = orderName.Length > 50 ? orderName.Substring(0, 50) : orderName;
        }

        DoPopulatePaymentProviderProperties(settings, ppNodeName, ppNode, customProperties, customPropertyList);
    }

    private void DoPopulatePaymentProviderProperties(
        PaymentSettings settings,
        string ppNodeName,
        object ppNode,
        object? customProperties,
        IEnumerable<PropertyInfo>? customPropertyList)
    {
        if (string.IsNullOrEmpty(settings.Language))
        {
            var prop = typeof(PaymentSettings).GetProperty(nameof(settings.EkomPropertyKeys))!;
            PopulateProperty(ppNode, settings, prop, null!);
        }
        if (string.IsNullOrEmpty(settings.Store))
        {
            var prop = typeof(PaymentSettings).GetProperty(nameof(settings.EkomPropertyKeys))!;
            PopulateProperty(ppNode, settings, prop, null!);
        }
        if (string.IsNullOrEmpty(settings.ConfigurationKey))
        {
            var prop = typeof(PaymentSettings).GetProperty(nameof(settings.ConfigurationKey))!;
            PopulateProperty(ppNode, settings, prop, null!);
        }

        if (!settings.EkomPropertyKeys.ContainsKey(PropertyEditorType.Language)
        || string.IsNullOrEmpty(settings.EkomPropertyKeys[PropertyEditorType.Language]))
        {
            throw new ArgumentException("String null or empty", nameof(PropertyEditorType.Language));
        }
        if (!settings.EkomPropertyKeys.ContainsKey(PropertyEditorType.Store)
        || string.IsNullOrEmpty(settings.EkomPropertyKeys[PropertyEditorType.Store]))
        {
            throw new ArgumentException("String null or empty", nameof(PropertyEditorType.Store));
        }

        PopulateProperties(
            ppNode,
            ppNodeName,
            settings,
            PaymentSettings.Properties,
            settings.EkomPropertyKeys);

        if (customProperties != null && customPropertyList != null)
        {
            ppNodeName = string.IsNullOrEmpty(settings.ConfigurationKey) ? ppNodeName
                : settings.ConfigurationKey;

            PopulateProperties(
                ppNode,
                ppNodeName,
                customProperties,
                customPropertyList,
                settings.EkomPropertyKeys,
                ppNodeName);
        }
    }

    private void PopulateProperties(
        object ppNode,
        string ppNodeName,
        object o,
        IEnumerable<PropertyInfo> properties,
        Dictionary<PropertyEditorType, string> ekomPropertyKeys,
        string? configSection = null)
    {
        _logger.LogDebug(
            "Populating {SettingsType} properties for {PaymentProvider}",
            o.GetType().Name,
            ppNodeName);

        foreach (PropertyInfo property in properties)
        {
            PopulateProperty(ppNode, o, property, ekomPropertyKeys, configSection);
        }
    }

    private void PopulateProperty(
        object ppNode,
        object o,
        PropertyInfo property,
        Dictionary<PropertyEditorType, string> ekomPropertyKeys,
        string? configSection = null)
    {
        _logger.LogTrace("Populating property {PropertyName} on {SettingsType}", property.Name, o.GetType().Name);

        object val = property.GetValue(o);
        if (val != null && property.PropertyType.IsValueType
            ? val.Equals(Activator.CreateInstance(property.PropertyType))
            : val == null)
        {
            var camelCaseName = property.Name.Substring(0, 1).ToLower() + property.Name.Substring(1);

            object? umbVal = null;
            var propAttribute = property.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(EkomPropertyAttribute));
            if (propAttribute != null)
            {
                var language = ekomPropertyKeys[PropertyEditorType.Language];
                var store = ekomPropertyKeys[PropertyEditorType.Store];

                var genericMethod = publishedContentValueMethod.MakeGenericMethod(typeof(string));
                var umbJsonVal = genericMethod.Invoke(
                    ppNode,
                    new object?[]
                    {
                        ppNode,
                        camelCaseName,
                        null,
                        null,
                        default(Fallback),
                        null
                    })
                    as string;

                if (!string.IsNullOrEmpty(umbJsonVal))
                {
                    var propVal = JsonConvert.DeserializeObject<PropertyValue>(umbJsonVal);

                    string ekomPropertyKey = string.Empty; 
                    var attr = property.GetCustomAttribute<EkomPropertyAttribute>()!;
                    if (attr.PropertyEditorType == PropertyEditorType.Language)
                    {
                        ekomPropertyKey = ekomPropertyKeys[PropertyEditorType.Language];
                    }
                    else if (attr.PropertyEditorType == PropertyEditorType.Store)
                    {
                        ekomPropertyKey = ekomPropertyKeys[PropertyEditorType.Store];
                    }
                    else
                    {
                        throw new NotSupportedException("Unsupported ekom property attribute");
                    }

                    if (propVal?.Values?.ContainsKey(ekomPropertyKey) == true)
                    {
                        var umbPropVal = propVal.Values
                            .FirstOrDefault(x => x.Key == ekomPropertyKey).Value
                            ?.ToString();

                        if (!string.IsNullOrEmpty(umbPropVal))
                        {
                            if (property.PropertyType == typeof(Uri))
                            {
                                var uri = ParseUri(umbPropVal);
                                property.SetValue(o, uri);
                            }
                            else
                            {
                                umbVal = PaymentsTypeConverter.ConvertValue(property.PropertyType, umbPropVal);
                            }
                        }
                    }
                }
            }
            else
            {
                var genericMethod = publishedContentValueMethod.MakeGenericMethod(property.PropertyType);
                umbVal = genericMethod.Invoke(
                    ppNode,
                    new object?[]
                    {
                        ppNode,
                        camelCaseName,
                        null,
                        null,
                        default(Fallback),
                        null
                });
            }

            if (umbVal != default)
            {
                property.SetValue(o, umbVal);
            }
            else
            {
               var paymentsSection = _configuration.GetSection("Ekom:Payments");

                if (configSection != null && paymentsSection != null)
                {
                    paymentsSection = paymentsSection.GetSection(configSection);
                }

                if (property.PropertyType == typeof(Uri))
                {
                    var configVal = paymentsSection.GetValue(typeof(string), camelCaseName)
                        as string;

                    if (!string.IsNullOrEmpty(configVal))
                    {
                        var uri = ParseUri(configVal);
                        property.SetValue(o, uri);
                    }
                }
                else
                {
                    var configVal = paymentsSection.GetValue(property.PropertyType, camelCaseName);

                    property.SetValue(o, configVal);
                }
            }
        }
    }

    private Uri ParseUri(string uriString)
    {
        var req
            = _httpContextAccessor.HttpContext?.Request
            ?? throw new Exception("Missing required HttpContext during property population");

        return PaymentsUriHelper.EnsureFullUri(uriString, req);
    }
}
