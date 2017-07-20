using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace uWebshop.Services
{
    public class ObjectFactory
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
