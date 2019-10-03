using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Exceptions
{
    /// <summary>
    /// 
    /// </summary>
    public class EnsureNodesException : Exception
    {
        /// <summary>
        /// 
        /// </summary>
        public EnsureNodesException()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public EnsureNodesException(string message) 
            : base(message)
        {
        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException"></param>
        public EnsureNodesException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
