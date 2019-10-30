using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Utilities
{
    static class GuidUdiHelper
    {
        public static Guid GetGuid(string guidUdi)
        {
            Uri uri;

            if (Uri.IsWellFormedUriString(guidUdi, UriKind.Absolute) == false
                || Uri.TryCreate(guidUdi, UriKind.Absolute, out uri) == false)
            {
                throw new FormatException(string.Format("String \"{0}\" is not a valid udi.", guidUdi));
            }

            var guidStr = uri.AbsolutePath.TrimStart('/');

            return new Guid(guidStr);
        }
    }
}
