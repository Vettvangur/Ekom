using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Ekom.Payments;

/// <summary>Runs every <see cref="IPaymentModule"/> of the installed payment provider packages.</summary>
public static class PaymentModuleLoader
{
    /// <summary>Loads the assemblies that depend on Ekom and lets their payment modules register services.</summary>
    public static IServiceCollection AddEkomPaymentModules(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var moduleType in FindModuleTypes())
        {
            if (Activator.CreateInstance(moduleType) is IPaymentModule module)
            {
                module.ConfigureServices(services, configuration);
            }
        }
        return services;
    }

    internal static IReadOnlyList<Type> FindModuleTypes()
    {
        var ekom = typeof(IPaymentModule).Assembly.GetName().Name;
        var assemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        // Provider assemblies are usually not loaded yet at registration time: load the ones
        // in the application folder that reference Ekom. Only metadata is read to decide.
        foreach (var path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            try
            {
                if (!ReferencesAssembly(path, ekom)) continue;
                var assembly = Assembly.Load(AssemblyName.GetAssemblyName(path));
                assemblies[assembly.FullName ?? path] = assembly;
            }
            catch (Exception ex) when (ex is IOException or BadImageFormatException or FileLoadException or UnauthorizedAccessException)
            {
                // Native or unreadable files have no modules to offer.
            }
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            if (assembly.GetReferencedAssemblies().Any(x => x.Name == ekom)) assemblies[assembly.FullName ?? assembly.GetName().FullName] = assembly;
        }

        var modules = new List<Type>();
        foreach (var assembly in assemblies.Values)
        {
            Type?[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types; }
            modules.AddRange(types.Where(x => x is { IsAbstract: false, IsInterface: false }
                && typeof(IPaymentModule).IsAssignableFrom(x) && x.GetConstructor(Type.EmptyTypes) != null)!);
        }
        return modules.OrderBy(x => x.FullName, StringComparer.Ordinal).ToList();
    }

    private static bool ReferencesAssembly(string path, string? name)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        if (!pe.HasMetadata) return false;
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.AssemblyReferences)
        {
            if (reader.GetString(reader.GetAssemblyReference(handle).Name) == name) return true;
        }
        return false;
    }
}
