using Ekom.API;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Hosting;
using Umbraco.Core.Logging;

namespace Ekom.Services
{
    class CheckoutService
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IDiscountStockRepository _discountStockRepo;
        readonly IOrderRepository _orderRepo;
        readonly ICouponRepository _couponRepo;
        readonly OrderService _orderService;
        readonly MailService _mailService;
        public CheckoutService(
            ILogger logger,
            Configuration config,
            IOrderRepository orderRepo,
            ICouponRepository couponRepo,
            OrderService orderService,
            IDiscountStockRepository discountStockRepo, 
            MailService mailService)
        {
            _logger = logger;
            _config = config;
            _orderRepo = orderRepo;
            _couponRepo = couponRepo;
            _orderService = orderService;
            _discountStockRepo = discountStockRepo;
            _mailService = mailService;
        }

        public async Task CompleteAsync(Guid key)
        {
            OrderData o = null;
            OrderInfo oi = null;

            try
            {
                _logger.Info<CheckoutService>("Completing order {OrderId}", key);

                o = await _orderRepo.GetOrderAsync(key).ConfigureAwait(false);

                if (o == null) throw new ArgumentException("Unable to find order with key: " + key, nameof(key));

                oi = new OrderInfo(o);

                // Currently unused
                foreach (var job in oi.HangfireJobs)
                {
                    Stock.Instance.CancelRollback(job);
                }

                await ProcessOrderLinesStockAsync(oi).ConfigureAwait(false);

                if (oi.Discount != null)
                {
                    try
                    {
                        //discount eventar virka ekki baun (vilt líklega hlusta frekar eftir coupon, þurfum þá coupon klasa og henda honum á orderinfo og orderline og breyta "öllu")
                        //oi.Discount?.OnCouponApply();

                        if (!string.IsNullOrEmpty(oi.Coupon))
                        {
                            await _couponRepo.MarkUsedAsync(oi.Coupon)
                                .ConfigureAwait(false);
                        }
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex) // Swallow all event subscriber exceptions
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        _logger.Error<CheckoutService>(ex);
                    }
                }

                foreach (var line in oi.OrderLines.Where(line => line.Discount != null))
                {
                    if (!string.IsNullOrEmpty(line.Coupon))
                    {
                        var id = $"{line.Discount.Key}_{line.Coupon}";
                        await _discountStockRepo.UpdateAsync(id, -1)
                            .ConfigureAwait(false);
                    }

                    if (line.Discount.HasMasterStock)
                    {
                        await _discountStockRepo.UpdateAsync(line.Discount.Key.ToString(), -1)
                            .ConfigureAwait(false);
                    }

                    try
                    {
                        //discount eventar virka ekki baun (vilt líklega hlusta frekar eftir coupon, þurfum þá coupon klasa og henda honum á orderinfo og orderline og breyta "öllu")
                        //line.Discount?.OnCouponApply();
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception ex) // Swallow all event subscriber exceptions
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        _logger.Error<CheckoutService>(ex);
                    }
                }

                await _orderService.ChangeOrderStatusAsync(o.UniqueId, OrderStatus.ReadyForDispatch)
                    .ConfigureAwait(false);
            }
            catch (NotEnoughStockException ex)
            {
                _logger.Error<CheckoutService>(ex, $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                    + $"Order id: {oi?.UniqueId}");

                _mailService.Recipient = _config.EmailNotifications ?? _mailService.Recipient;
                _mailService.Subject = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                    + $"Order id: {oi?.UniqueId}";
                var body = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}."
                    + $"Order id: {oi?.UniqueId}\r\n";

                if (ex is NotEnoughLineStockException exl)
                {
                    body += $"Line {exl.OrderLineKey} and variant == {exl.Variant == true}";
                }

                body += ex.ToString();
                _mailService.Body = body;

                HostingEnvironment.QueueBackgroundWorkItem(
                    async ct => await _mailService.SendAsync().ConfigureAwait(false));
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error<CheckoutService>(ex);
                _mailService.Recipient = _config.EmailNotifications ?? _mailService.Recipient;
                _mailService.Subject = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                    + $"Order id: {oi?.UniqueId}";
                _mailService.Body = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}."
                    + $"Order id: {oi?.UniqueId}\r\n\r\n" + ex.ToString();

                HostingEnvironment.QueueBackgroundWorkItem(
                    async ct => await _mailService.SendAsync().ConfigureAwait(false));
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task ProcessOrderLinesStockAsync(IOrderInfo order)
        {
            foreach (var line in order.OrderLines)
            {
                if (!line.Product.Backorder)
                {
                    if (line.Product.VariantGroups.Any())
                    {
                        foreach (var variant in line.Product.VariantGroups.SelectMany(x => x.Variants))
                        {
                            var variantStock = Stock.Instance.GetStock(variant.Key);

                            if (variantStock >= line.Quantity)
                            {
                                await Stock.Instance.IncrementStockAsync(variant.Key, (line.Quantity * -1))
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                _logger.Error<CheckoutService>($"Stock error one line {line.Key} with variant {variant.Key}");
                                throw new NotEnoughLineStockException("Stock error ")
                                {
                                    OrderLineKey = line.Key,
                                    Variant = true,
                                };
                            }
                        }
                    }
                    else
                    {
                        var productStock = Stock.Instance.GetStock(line.ProductKey);

                        if (productStock >= line.Quantity)
                        {
                            //if (_config.ReservationTimeout.Seconds <= 0)
                            //{
                            //    await Stock.Instance.ReserveStockAsync(line.ProductKey, (line.Quantity * -1));
                            //}
                            //else
                            //{
                            //    hangfireJobs.Add(await Stock.Instance.ReserveStockAsync(line.ProductKey, (line.Quantity * -1)));
                            //}
                            await Stock.Instance.IncrementStockAsync(line.ProductKey, (line.Quantity * -1))
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.Error<CheckoutService>($"Stock error one line {line.Key} with product {line.ProductKey}");
                            throw new NotEnoughLineStockException("Stock error ")
                            {
                                OrderLineKey = line.Key,
                            };
                        }
                    }
                }

                // How does this work ? we dont have a coupon per orderline!
                //if (line.Discount != null)
                //{
                //    hangfireJobs.Add(_stock.ReserveDiscountStock(line.Discount.Key, 1, line.Coupon));

                //    if (line.Discount.HasMasterStock)
                //    {
                //        hangfireJobs.Add(_stock.ReserveDiscountStock(line.Discount.Key, 1));
                //    }
                //}
            }
        }
    }
}
