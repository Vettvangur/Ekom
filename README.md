## Ekom

## Ekom request in surface controllers / api controllers

We register an IHttpModule to ensure creation of an uwbsRequest entry in the runtimeCache.

The module listens for incoming requests containing a store querystring parameter.

This means controller actions do not need to ask for a store param as the module will read it from the request uri

and create the uwbsRequest object with store domainprefix and currency if applicable.


## Discount Notes

#### Terminology
1. Order discounts, applied to OrderInfo, can have coupons, can be inclusive/exclusive (meaning they can or cant be applied at the same time as product discounts in the same order)
  - When applying order discounts, we test constraints. We do not check discountItems.
  - Checking discount items for order discounts would not be a stand-in for complex checks such as "minimum of 3 pants".
  - It would also not make the order discount magically apply only to select order lines, we use product discounts for that
2. Product discounts, can be global (meaning they automatically apply to new OrderLines on creation if their conditions are met)
  - Can have coupons?

##### Rules for choosing a discount in OrderInfo/OrderLine for calculating price are the following:
1. Ensure constraints verify
2. If the line has a discount, use that
3. If the line does not have a discount but the order has one and it applies to the product on the given OrderLine, use that
4. If neither then no discount :)

##### Rules for applying a discount to OrderInfo
1. Ensure constraints verify
2. If there was no discount previously on OrderInfo...
- If stackable, apply discount
- If non-stackable and there are no line discounts, apply discount
- If non-stackable, and there are line discounts applied, apply discount to OrderInfo temporarily. Then compare old and new order total. If the new discount compares favorably, remove the line discounts and apply the new order discount.
  - OrderInfo does not use line discount when a non-stackable discount is set, this is only for this specific case of comparing.
3. If there is one, compare (using non-stackable compare above if applicable) and apply new Discount if better

##### When do ProductDiscounts get applied?
When a new OrderLine is created Product.Discount is called, if it returns a match we check the rules above and apply if applicable
	
##### Due to the aforementioned rules it's questionable if ApplyDiscountToOrderLineAsync should accept IDiscount versus only accepting IProductDiscount
With the current discount options only IDiscount can go to OrderInfo and IProductDiscount are applied straight to OrderLine's
Some day we might want to allow a type that applies manually to OrderLine's with a coupon, allowing stacking with an OrderInfo discount f.x. ?

##### When applying a discount to OrderLine's
We make sure that the current OrderInfo discount, if there is one, allows stacking
	
##### What is stacking
Applying discounts to specific OrderLine's while applying a seperate discount to the order and general order items

##### ProductDiscount should only apply to applicable OrderLine's - OrderInfo never has a ProductDiscount
ApplyDiscountToOrder does not accept IProductDiscount currently
 * we always apply IProductDiscount straight to OrderLines
Points to consider for allowing OrderInfo to have ProductDiscounts
 * We could also create a ApplyDiscountToOrder accepting IProductDiscount. It would then apply the ProductDiscount straight to applicable OrderLine's
 * If we push the discount from OrderInfo to OrderLine on creation of line, how would 'exclusive' work
 * Example: OrderInfo has ProductDiscount, applicable line gets discount. An inclusive global discount is applied to Order, old OrderLine keeps product discount. Now new OrderLine's do not get the initial ProductDiscount !!

##### Caveats:
We still have no "Real" order discounts, that is it is not currently possible to create a fixed order discount that gives free shipping the discount is applied per OrderLine and gets multiplied by the amount of lines
	
ekmDiscount do not currently support DiscountItems. If you successfully apply a coupon or discount to OrderInfo it will be applied to all OrderLines and all ekmProductDiscount get auto added to products so they won't work with coupons
(previously ekmProductDiscount's were also all cached and displayed on Product.Price if applicable)
			
This means there is no current way to apply a coupon to an order and get a discount for only select items.
Either all items get the discount or the discount is not coupon based and got auto-applied to OrderLines if applicable
