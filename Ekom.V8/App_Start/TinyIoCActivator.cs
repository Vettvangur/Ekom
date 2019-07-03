using CommonServiceLocator.TinyIoCAdapter;
using TinyIoC;

[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(EkomV8.App_Start.TinyIoCActivator), "Start")]
[assembly: WebActivatorEx.ApplicationShutdownMethod(typeof(EkomV8.App_Start.TinyIoCActivator), "Shutdown")]

namespace EkomV8.App_Start
{
    /// <summary>Provides the bootstrapping for integrating TinyIoC when it is hosted in ASP.NET</summary> 
    static class TinyIoCActivator
    {
        /// <summary>Integrates TinyIoC when the application starts.</summary> 
        public static TinyIoCContainer Start()
        {
            // Register Types 
            var container = TinyIoCContainer.Current;
            Configuration.container = new TinyIoCServiceLocator(container);

            return container;
        }

        /// <summary>Disposes the container when the application is shut down.</summary> 
        public static void Shutdown()
        {
            var container = TinyIoCContainer.Current;
            container.Dispose();
        }
    }
}
