using System;

namespace EkomV8.IoC
{
    public interface IContainerRegistration
    {
        Lifetime Lifetime { get; }
        Type Type { get; }
    }
}
