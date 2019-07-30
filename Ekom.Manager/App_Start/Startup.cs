using Ekom.Interfaces;
using Ekom.Repository;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Umbraco.Extensions.Events
{
    class EkomEvents : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.Components()
                .Append<RegisterCustomBackofficeMvcRouteComponent>()
                ;

            composition.Register<IManagerRepository, ManagerRepository>(Lifetime.Transient);
        }
    }
}
