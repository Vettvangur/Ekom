using CommonServiceLocator;
using System;

namespace Ekom.IoC
{

    /// <summary>
    /// Represents a registration to be added to a container
    /// </summary>
    public class ContainerRegistration : IContainerRegistration
    {
        public ContainerRegistration(Lifetime lifetime, Type type)
        {
            Lifetime = lifetime;
            Type = type;
        }

        public Lifetime Lifetime { get; private set; }
        public Type Type { get; private set; }
    }

    /// <summary>
    /// Represents a registration to be added to a container
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ContainerRegistration<T> : ContainerRegistration, IActivatorContainerRegistration
    {
        public ContainerRegistration(Lifetime lifetime) : base(lifetime, typeof(T))
        {
        }

        public ContainerRegistration(Lifetime lifetime, Func<IServiceLocator, object> activator) : base(lifetime, typeof(T))
        {
            Activator = activator;
        }

        public ContainerRegistration(Func<IServiceLocator, object> activator) : base(Lifetime.ExternallyOwned, typeof(T))
        {
            Activator = activator;
        }

        public Func<IServiceLocator, object> Activator { get; private set; }
    }
}
