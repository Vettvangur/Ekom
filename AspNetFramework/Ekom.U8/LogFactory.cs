using Microsoft.Extensions.Logging;
using System;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Ekom.U8
{
    /// <summary> 
    /// Creates an <see cref="ILog"/> instance for the provided <see cref="Type"/> 
    /// </summary> 
    class LogFactory
    {
        readonly IFactory _factory;

        public LogFactory(IFactory factory)
        {
            _factory = factory;
        }

        /// <summary> 
        ///  
        /// </summary> 
        /// <param name="T">Type of class this logger logs for</param> 
        /// <returns></returns> 
        public ILogger GetLogger(Type T)
        {
            var logger = _factory.GetInstance<Umbraco.Core.Logging.ILogger>();
            return new UmbracoLogger(logger, T);
        }

        /// <summary> 
        ///  
        /// </summary> 
        /// <param name="T">Type of class this logger logs for</param> 
        /// <returns></returns> 
        public ILogger GetLogger<T>()
        {
            var logger = _factory.GetInstance<Umbraco.Core.Logging.ILogger>();
            return new UmbracoLogger(logger, typeof(T));
        }
    }
}
