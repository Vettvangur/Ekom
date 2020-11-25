using Umbraco.NetPayment;
using System;

namespace Ekom.Extensions
{
    public static class LocalCallback
    {
        /// <summary>
        /// Raises the success event on successful payment verification
        /// </summary>
        /// <param name="o"></param>
        internal static void OnSuccess(OrderStatus o)
        {
            Success?.Invoke(o);
            LocalCallbacks.OnSuccess(o);
        }

        /// <summary>
        /// Raises the success event on successful payment verification
        /// </summary>
        /// <param name="o"></param>
        /// <param name="ex"></param>
        internal static void OnError(OrderStatus o, Exception ex)
        {
            Error?.Invoke(o, ex);
            LocalCallbacks.OnError(o, ex);
        }

        /// <summary>
        /// Event fired on successful payment verification
        /// </summary>
        public static event LocalCallbacks.successCallback Success;
        /// <summary>
        /// Event fired on payment verification error
        /// </summary>
        public static event LocalCallbacks.errorCallback Error;
    }
}
