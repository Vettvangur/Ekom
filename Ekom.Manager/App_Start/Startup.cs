using Ekom.Interfaces;
using Ekom.Manager.App_Start;
using Ekom.Manager.Controllers;
using Ekom.Repository;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Web;

namespace Umbraco.Extensions.Events
{
    class EkomEvents : IUserComposer
    {
        public void Compose(Composition composition)
        {
            composition.Components()
                .Append<RegisterCustomBackofficeMvcRouteComponent>();

            composition.Register<EkomController>(Lifetime.Request);
            composition.Register<IManagerRepository, ManagerRepository>(Lifetime.Transient);
            composition.Sections().Append<EkomSection>();
        }
    }
}
