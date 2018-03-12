using CommonServiceLocator;
using System;

namespace Ekom.IoC
{
    public interface IActivatorContainerRegistration
    {
        Func<IServiceLocator, object> Activator { get; }
    }
}
