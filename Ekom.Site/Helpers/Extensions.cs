using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net;

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
        public static bool IsLocalhost(string hostNameOrAddress)
        {
            if (string.IsNullOrEmpty(hostNameOrAddress))
                return false;

            try
            {
                // get host IP addresses
                IPAddress[] hostIPs = Dns.GetHostAddresses(hostNameOrAddress);
                // get local IP addresses
                IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
                // test if any host IP is a loopback IP or is equal to any local IP
                return hostIPs.Any(hostIP => IPAddress.IsLoopback(hostIP) || localIPs.Contains(hostIP));
            }
            catch
            {
                return false;
            }
        }
    }
}
