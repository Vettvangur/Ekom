using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Payments.Valitor;

[Serializable]
public class ValitorAHKException : Exception
{
    public ValitorAHKException() { }
    public ValitorAHKException(string message) : base(message) { }
    public ValitorAHKException(string message, Exception inner) : base(message, inner) { }
    protected ValitorAHKException(
      System.Runtime.Serialization.SerializationInfo info,
      System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}
