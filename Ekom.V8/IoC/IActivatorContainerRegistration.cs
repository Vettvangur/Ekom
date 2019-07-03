using CommonServiceLocator;
using System;

namespace EkomV8.IoC
{
    public interface IActivatorContainerRegistration
    {
        Func<IServiceLocator, object> Activator { get; }
    }
}
