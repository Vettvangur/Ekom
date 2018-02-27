using System;

namespace Ekom.IoC
{
    public interface IContainerRegistration
    {
        Lifetime Lifetime { get; }
        Type Type { get; }
    }
}
