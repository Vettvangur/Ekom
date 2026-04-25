# Checkout Overview

Checkout in Ekom is the process of taking the current order from a cart state into payment submission and final completion.

In practice, checkout is not a single action. It is a sequence of updates on the current order:

1. load the current order
2. update customer information
3. select shipping provider
4. select payment provider
5. submit the order to payment
6. complete the order

## What checkout includes in Ekom

During checkout, Ekom may:

- update customer data on the order
- attach shipping and payment providers
- update tracking and consent
- validate stock
- update order status
- write activity logs
- raise checkout and order events

Checkout starts before payment is submitted.

Selecting providers or updating customer information is part of checkout, but those actions do **not** complete the order by themselves.

## Typical checkout stages

## 1. Current order

Checkout begins with the current order.

This order already contains the products or variants the customer wants to buy.

## 2. Customer information

Customer email, name, phone, address, and other checkout fields are added to the order.

This is usually where checkout starts to become provider-ready.

## 3. Shipping provider

The selected shipping provider is attached to the order.

This affects totals, shipping data, and later checkout behavior.

## 4. Payment provider

The selected payment provider is attached to the order.

This prepares the order for payment submission, but does not by itself submit payment.

## 5. Payment handoff

The order is submitted into the payment flow.

In Razor or server-rendered solutions, this is usually done through `API.Order.PayAsync(...)`.

In headless solutions, this is usually done through `POST /ekom/checkout/pay`.

## 6. Completion

Once payment or offline checkout flow is finished, the order is completed.

Completion is where Ekom performs the final stock, status, logging, and checkout completion work.

## Razor vs headless checkout

Ekom supports both server-rendered and headless checkout implementations.

### Razor / server-rendered

In a Razor-based setup, you usually:

- read the current order through `API.Order`
- post forms back into Ekom endpoints or call APIs directly
- submit payment with `PayAsync(...)`
- redirect the user through the payment flow

### Headless

In a headless setup, you usually:

- load products, cart, and providers through HTTP endpoints
- update the order through `/ekom/order/...` endpoints
- submit payment through `/ekom/checkout/pay`
- handle success or return flow in your frontend

## Recommended page order

Read the checkout docs in this order:

1. [Checkout Flow](checkout-flow.md)
2. [Payment Provider Selection](payment-provider-selection.md)
3. [Shipping Provider Selection](shipping-provider-selection.md)
4. [Complete Checkout](complete-checkout.md)
5. [Checkout Endpoints](checkout-endpoints.md)

## Related pages

- [Checkout Flow](checkout-flow.md)
- [Complete Checkout](complete-checkout.md)
- [Payment Provider Selection](payment-provider-selection.md)
- [Shipping Provider Selection](shipping-provider-selection.md)
- [Order API](api-order-reference.md)
- [Checkout Endpoints](checkout-endpoints.md)
