using System;
using System.Collections.Generic;
using System.Linq;
using TinyIoC;

namespace CommonServiceLocator.TinyIoCAdapter
{
    class TinyIoCServiceLocator : IServiceLocator
    {
        private readonly TinyIoCContainer _container;

        public TinyIoCServiceLocator(TinyIoCContainer container)
        {
            _container = container ?? TinyIoCContainer.Current;
        }

        public TinyIoCServiceLocator() : this(null) { }

        public IEnumerable<TService> GetAllInstances<TService>()
        {
            return _container.ResolveAll(typeof(TService), true).Cast<TService>();
        }

        public IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return _container.ResolveAll(serviceType, true);
        }

        public TService GetInstance<TService>(string key)
        {
            return (TService)_container.Resolve(typeof(TService), key);
        }

        public TService GetInstance<TService>()
        {
            return (TService)_container.Resolve(typeof(TService));
        }

        public object GetInstance(Type serviceType, string key)
        {
            return _container.Resolve(serviceType, key);
        }

        public object GetInstance(Type serviceType)
        {
            return _container.Resolve(serviceType);
        }

        public object GetService(Type serviceType)
        {
            return _container.Resolve(serviceType);
        }
    }
}
