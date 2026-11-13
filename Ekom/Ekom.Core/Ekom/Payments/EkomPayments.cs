using Ekom.Payments.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Reflection;

namespace Ekom.Payments;

/// <summary>
/// Finds the installed payment providers. A provider is any <see cref="IPaymentProvider"/>
/// with a constant named <c>_ppNodeName</c> holding its base payment provider name.
/// </summary>
public class EkomPayments
{
    private static readonly ConcurrentDictionary<string, Type> _paymentProviders = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _discoveryLock = new();

    private readonly ILogger<EkomPayments> _logger;
    private readonly IServiceProvider _serviceProvider;

    public EkomPayments(
        ILogger<EkomPayments> logger,
        PaymentsConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>Registers a payment provider type under its base payment provider name.</summary>
    public static void RegisterPaymentProvider(string name, Type providerType)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A provider name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(providerType);
        if (!typeof(IPaymentProvider).IsAssignableFrom(providerType))
            throw new ArgumentException($"{providerType} is not an {nameof(IPaymentProvider)}.", nameof(providerType));
        _paymentProviders[name] = providerType;
    }

    /// <summary>The names of the installed base payment providers.</summary>
    public static IReadOnlyCollection<string> PaymentProviderNames
    {
        get
        {
            Discover();
            return _paymentProviders.Keys.ToList();
        }
    }

    public IPaymentProvider GetPaymentProvider(string basePpName)
    {
        if (string.IsNullOrEmpty(basePpName))
        {
            throw new ArgumentException("string.IsNullOrEmpty", nameof(basePpName));
        }

        if (!_paymentProviders.TryGetValue(basePpName, out var provider))
        {
            // Provider assemblies may load after startup; look again before giving up.
            Discover();
            if (!_paymentProviders.TryGetValue(basePpName, out provider))
            {
                throw new PaymentProviderNotFoundException("Base Payment Provider not found. DLL possibly missing. Name: " + basePpName);
            }
        }

        return (IPaymentProvider)ActivatorUtilities.CreateInstance(_serviceProvider, provider);
    }

    private static void Discover()
    {
        lock (_discoveryLock)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type?[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }

                foreach (var type in types)
                {
                    if (type == null || type.IsInterface || type.IsAbstract || !typeof(IPaymentProvider).IsAssignableFrom(type)) continue;
                    var field = type.GetField("_ppNodeName", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field?.IsLiteral == true && field.GetRawConstantValue() is string name && !string.IsNullOrWhiteSpace(name))
                    {
                        _paymentProviders.TryAdd(name, type);
                    }
                }
            }
        }
    }
}
