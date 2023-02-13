using System;
using System.Web;
using System.Web.Mvc;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.NetPayment.Exceptions;
using Umbraco.NetPayment.Helpers;

namespace Umbraco.NetPayment.Valitor
{
    /// <summary>
    /// An alternative to subscribing to the valitor callback event.
    /// This helper can be invoked in the view or controller that receives the redirect from Valitor.
    /// Only returns <see cref="OrderStatus"/> on successful verification
    /// </summary>
    public class ValitorResponseHelper
    {
        /// <summary>
        /// 
        /// </summary>
        public static ValitorResponseHelper Instance =>
            Current.Factory.GetInstance<ValitorResponseHelper>();

        readonly ILogger _logger;
        readonly Settings _settings;
        readonly IDatabaseFactory _dbFac;
        readonly IXMLConfigurationService _xmlSvc;
        readonly HttpRequestBase _req;
        readonly IOrderService _orderSvc;

        /// <summary>
        /// ctor
        /// </summary>
        public ValitorResponseHelper(
            ILogger logger,
            Settings settings,
            IDatabaseFactory dbFac,
            IXMLConfigurationService xmlSvc,
            HttpRequestBase req,
            IOrderService orderSvc)
        {
            _logger = logger;
            _settings = settings;
            _dbFac = dbFac;
            _xmlSvc = xmlSvc;
            _req = req;
            _orderSvc = orderSvc;
        }

        /// <summary>
        /// Unfinished - don't use
        /// </summary>
        /// <param name="viewContext"></param>
        /// <returns></returns>
        [Obsolete("Unfinished!")]
        public OrderStatus Verify(ViewContext viewContext)
        {
            return null;
            // Map from HttpContext to valitor response object
            //Verify(responseobj);
        }

        /// <summary>
        /// Gets Order
        /// Only returns <see cref="OrderStatus"/> on successful verification
        /// </summary>
        public OrderStatus GetOrder(string reference)
        {
            if (!string.IsNullOrEmpty(reference)
            && Guid.TryParse(reference, out var guid))
            {
                return _orderSvc.GetAsync(guid).Result;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// An alternative to subscribing to the valitor callback event.
        /// This helper can be invoked in the view or controller that receives the redirect from Valitor.
        /// Only returns <see cref="OrderStatus"/> on successful verification
        /// </summary>
        public OrderStatus Verify(Response valitorResp, string ppNameOverride = null)
        {
            var xmlConfig = _xmlSvc.GetConfigForPP(ppNameOverride ?? Payment._ppNodeName, Payment._ppNodeName);
            if (xmlConfig == null) throw new XmlConfigurationNotFoundException(ppNameOverride ?? Payment._ppNodeName);

            string DigitalSignature = CryptoHelpers.GetMD5StringSum(xmlConfig["verificationcode"] + valitorResp.ReferenceNumber);

            if (valitorResp.DigitalSignatureResponse.Equals(DigitalSignature, StringComparison.InvariantCultureIgnoreCase))
            {
                if (Guid.TryParse(valitorResp.ReferenceNumber, out var guid))
                {
                    return _orderSvc.GetAsync(guid).Result;
                }
            }

            return null;
        }
    }
}
