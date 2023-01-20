using Ekom.API;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Utilities;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Ekom.Services
{
    /// <summary>
    /// Handles order finalisation after successful payment or order completion
    /// </summary>
    class CheckoutService
    {
        readonly ILogger<CheckoutService> _logger;
        readonly Configuration _config;
        readonly DiscountStockRepository _discountStockRepo;
        readonly OrderRepository _orderRepo;
        readonly CouponRepository _couponRepo;
        readonly OrderService _orderService;
        readonly IMailService _mailService;
        public CheckoutService(
            ILogger<CheckoutService> logger,
            Configuration config,
            OrderRepository orderRepo,
            CouponRepository couponRepo,
            OrderService orderService,
            DiscountStockRepository discountStockRepo,
            IMailService mailService)
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
                _logger.LogInformation("Completing order {OrderId}", key);

                o = await _orderRepo.GetOrderAsync(key).ConfigureAwait(false);

                if (o == null)
                {
                    return;
                }

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
                        _logger.LogError(ex, "Error on marking coupon used");
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
                        //_logger.LogError(ex);
                    }
                }

                await _orderService.ChangeOrderStatusAsync(o.UniqueId, OrderStatus.ReadyForDispatch)
                    .ConfigureAwait(false);
            }
            catch (NotEnoughStockException ex)
            {
                _logger.LogError(
                    ex, 
                    $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. " + 
                    $"Order id: {oi?.UniqueId}");

                var subject 
                    = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                    + $"Order id: {oi?.UniqueId}";
                var body 
                    = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}."
                    + $"Order id: {oi?.UniqueId}\r\n";

                if (ex is NotEnoughLineStockException exl)
                {
                    body += $"Line {exl.OrderLineKey} and variant == {exl.Variant == true}";
                }

                body += ex.ToString();

                await _mailService.SendAsync(subject, body).ConfigureAwait(false);

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected checkout error");
                var subject 
                    = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}. "
                    + $"Order id: {oi?.UniqueId}";
                var body 
                    = $"Unable to complete paid checkout for customer {o?.CustomerName} {o?.CustomerEmail}."
                    + $"Order id: {oi?.UniqueId}\r\n\r\n" + ex.ToString();

                await _mailService.SendAsync(subject, body).ConfigureAwait(false);
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
                                _logger.LogError($"Variant Stock error on line {line.Key} with variant {variant.Key}");
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
                            _logger.LogError($"Product Stock error one line {line.Key} with product {line.ProductKey}");
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
