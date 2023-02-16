using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ekom.Payments.Helpers;

static class TypeLoaderExtensions
{
    public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    {
        if (assembly == null) throw new ArgumentNullException("assembly");
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
#pragma warning disable CS8619 // Bugged IDE inference
            return e.Types.Where(t => t != null);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
        }
    }
}

static class TypeHelper
{
    public static IEnumerable<Type> GetConcreteTypesWithInterface(Assembly asm, Type myInterface)
    {
        return asm.GetLoadableTypes()
            .Where(
                x => myInterface.IsAssignableFrom(x)
                && !x.IsInterface
                && !x.IsAbstract)
            .ToList();
    }
}
