using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Ekom.Site.Helpers
{
    public static class Extensions
    {
        public static bool IsDebug()
        {
            if (HttpContext.Current != null)
            {
                if (HttpContext.Current.IsDebuggingEnabled)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
