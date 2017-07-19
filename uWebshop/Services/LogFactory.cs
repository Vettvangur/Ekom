using log4net;
using System;

namespace uWebshop.Services
{
    public class LogFactory : ILogFactory
    {
        public ILog GetLogger(Type T)
        {
            return LogManager.GetLogger(T);
        }
    }

    public interface ILogFactory
    {
        ILog GetLogger(Type T);
    }
}
