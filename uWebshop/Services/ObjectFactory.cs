using Microsoft.Practices.Unity;

namespace uWebshop.Services
{
    /// <summary>
    /// Unfinished
    /// </summary>
    class ObjectFactory
    {
        IUnityContainer _container;

        public ObjectFactory(IUnityContainer container)
        {
            _container = container;
        }

        //public T GetObject<T>(Store store)
        //{
        //    var o = _container.Resolve<T>();

        //    o
        //}
    }
}
