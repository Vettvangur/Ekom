using Ekom.Payments.Exceptions;
using Ekom.Payments;
using Examine;
using Examine.Search;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Web.Common;
using Umbraco.Extensions;
using static Umbraco.Cms.Core.Constants;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Linq.Expressions;
using Org.BouncyCastle.Ocsp;
using Ekom.Payments.Helpers;
using Microsoft.AspNetCore.Http;
using System.ComponentModel;

namespace Ekom.Payments.Umb;

class UmbracoService : IUmbracoService
{
    readonly ILogger<UmbracoService> _logger;
    readonly IConfiguration _configuration;
    readonly IExamineManager _examineManager;
    readonly IUmbracoHelperAccessor _umbracoHelperAccessor;
    readonly IHttpContextAccessor _httpContextAccessor;

    static MethodInfo publishedContentValueMethod = typeof(FriendlyPublishedContentExtensions)
        .GetMethods()
        .Where(x => x.Name == "Value" && x.IsGenericMethodDefinition)
        .First()
        ;

    public UmbracoService(
        IExamineManager examineManager,
        IUmbracoHelperAccessor umbracoHelperAccessor,
        IConfiguration configuration,
        ILogger<UmbracoService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _examineManager = examineManager;
        _umbracoHelperAccessor = umbracoHelperAccessor;
        _configuration = configuration;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private ISearchResult? GetProviderContainer()
    {
        if (!_examineManager.TryGetIndex(UmbracoIndexes.ExternalIndexName, out var index))
        {
            throw new ExternalIndexNotFoundException("Could not access the Examine searcher");
        }

        var searchResults = index
            .Searcher
            .CreateQuery("content")
            .NodeTypeAlias(PaymentsConfiguration.ContainerDocumentTypeAlias)
            .Execute();

        if (searchResults?.Any() != true)
        {
            throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider container node");
        }

        return searchResults.First();
    }

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
        var container = GetProviderContainer();

        if (!_umbracoHelperAccessor.TryGetUmbracoHelper(out var umbracoHelper))
        {
            throw new NetPaymentException("Could not access the Umbraco helper");
        }

        var ppContainer = umbracoHelper.Content(container.Id);

        if (ppContainer == null) throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider container node");

        var visibleChildren = ppContainer.Children.Where(x => x.IsVisible());

        return visibleChildren.FirstOrDefault(x =>
                   x.Name.Equals(ppNodeName, StringComparison.InvariantCultureIgnoreCase))
               ?? visibleChildren.First(x =>
                   x.ContentType.Alias.Equals(ppNodeName, StringComparison.InvariantCultureIgnoreCase));
    }

    private IPublishedContent GetPPNode(Guid key)
    {
        if (!_umbracoHelperAccessor.TryGetUmbracoHelper(out var umbracoHelper))
        {
            throw new NetPaymentException("Could not access the Umbraco helper");
        }

        if (!_examineManager.TryGetIndex(UmbracoIndexes.ExternalIndexName, out var index))
        {
            throw new Exception("Could not access the Examine member searcher");
        }

        var searchResults = index
            .Searcher
            .CreateQuery("content")
            .NativeQuery($"+__Key:\"{key}\"")
            .Execute();

        var result = searchResults.FirstOrDefault();
        if (result == null) throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider node");

        var ppNode = umbracoHelper.Content(result.Id);
        if (ppNode == null) throw new PaymentProviderNotFoundException("Unable to find Umbraco payment provider node");

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

            settings.OrderName = sb.ToString().TrimEnd(' ');
        }

        DoPopulatePaymentProviderProperties(settings, ppNodeName, ppNode, customProperties, customPropertyList);
    }

    //public void PopulatePaymentProviderProperties(
    //    PaymentSettings settings,
    //    Guid ppKey,
    //    object? customProperties,
    //    IEnumerable<PropertyInfo>? customPropertyList)
    //{
    //    _logger.LogDebug("Populating properties for {PaymentProvider}", ppKey);

    //    settings.PaymentProviderKey = ppKey;

    //    Lazy<IPublishedContent> ppNode = new Lazy<IPublishedContent>(
    //        () => GetPPNode(ppKey)
    //    );

    //    DoPopulatePaymentProviderProperties(settings, ppKey.ToString(), ppNode, customProperties, customPropertyList);
    //}

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
        if (property.PropertyType.IsValueType
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
                        // ToDo: If we use some reflection magic here as well or refactor this to a dedicated method
                        // we could have this whole monster of a method and it's friends in the core library
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
                            //if (property.PropertyType == typeof(Guid))
                            //{
                            //    if (Guid.TryParse(umbPropVal, out var guid))
                            //    {
                            //        umbVal = guid;
                            //    }
                            //}
                            //else if (property.PropertyType == typeof(int))
                            //{
                            //    if (int.TryParse(umbPropVal, out var myInt))
                            //    {
                            //        umbVal = myInt;
                            //    }
                            //}
                            //else if (property.PropertyType == typeof(bool))
                            //{
                            //    if (bool.TryParse(umbPropVal, out var myBool))
                            //    {
                            //        umbVal = myBool;
                            //    }
                            //}
                            /*else*/ if (property.PropertyType == typeof(Uri))
                            {
                                var uri = ParseUri(umbPropVal);
                                property.SetValue(o, uri);
                            }
                            //else if (property.PropertyType.IsEnum)
                            //{
                            //    var enumVal = Enum.Parse(property.PropertyType, umbPropVal);
                            //    property.SetValue(o, enumVal);
                            //}
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
