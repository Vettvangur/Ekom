# Coupons

[Ekom docs](../../README.md) / [Guides](../README.md) / [Discounts and coupons](../discounts.md) / Coupons

A coupon code switches on an order discount when the customer enters it. The code itself holds no amount and no products: those come from the order discount it belongs to. Use coupons for a shared campaign code such as `SPRING10`, for single-use codes that you send to customers, or for any offer that the customer must ask for.

On this page:

- [Set up the order discount first](#set-up-the-order-discount-first)
- [Add a shared code](#add-a-shared-code)
- [Understand usage limits](#understand-usage-limits)
- [Generate many codes at once](#generate-many-codes-at-once)
- [Find, export and delete codes](#find-export-and-delete-codes)
- [Usage limits are not discount stock](#usage-limits-are-not-discount-stock)
- [Test before you send out codes](#test-before-you-send-out-codes)

## Set up the order discount first

1. Create an order discount as described in [Create an order discount](order-discounts.md#create-an-order-discount).
2. Fill in **Type**, **Discount**, **Discount Items**, any exclusions and any quantity rule.
3. Leave **Global Discount** off.
4. Save the discount. The **Coupons** tab shows "You need to save the order discount before you can add coupon codes" until you do. After the first save, refresh the page.
5. Open the **Coupons** tab.

Do not add codes to an automatic offer (Global Discount on). An order discount with any code is never applied automatically. If you need both an automatic offer and a code offer, create two order discounts.

## Add a shared code

1. On the **Coupons** tab, select **Add Code**.
2. Fill in the two fields:

   | Field | What it means | Example |
   | --- | --- | --- |
   | Coupon Code | The code customers enter. | `SPRING10` |
   | Usage limits | How many uses the code has left, for all customers together. The default is `1`. | `100` for 100 uses in total |

3. Select **Add**. The code appears in the list with the message "Coupon Code added successfully."

The code is saved as soon as you select **Add**. You do not need to publish the discount again for the code. The discount itself must be published.

Each code can exist only once, across all discounts. If the code is already in use, you see "Coupon Code already exists on this or another discount."

Ekom saves codes in lower case. Customers can type the code in upper or lower case: `SPRING10`, `spring10` and `Spring10` are the same code.

A code works in a store only when its order discount runs in that store. If **Disable** is on for a store, the code does not work there.

### Example: 10% off selected products for the first 100 uses

**Goal:** a shared campaign code that gives 10% off selected products and can be used 100 times in total.

| Field | Value |
| --- | --- |
| Type (order discount) | Percentage |
| Discount | `10` |
| Discount Items | The campaign products or category |
| Global Discount | Off |
| Stackable | Off, unless the 10% must also apply on top of sale prices |
| Quantity Discount Mode | None |
| Coupon Code | `SPRING10` |
| Usage limits | `100` |

**Result:** when a customer enters `SPRING10` on a basket with campaign products, those products get 10% off. Other products do not change. If the basket already has an order discount that gives a lower total, the code is not applied. When an order with the code completes, the code has one use less.

This does not mean 100 uses per customer, and it does not mean 100 different customers. One customer can use the code on several orders.

## Understand usage limits

> **Usage limits belong to the code, not to a customer.** Ekom does not look at the customer, the email address, the user name or the login when it counts uses. A limit of `1` means one use in total, not one use per customer.

The **Usage limits** column shows how many uses the code has left. It goes down by 1 each time an order that used the code completes. A code that starts at `10` shows `9` after one completed order.

| Usage limits shows | Meaning |
| --- | --- |
| `1` | One use left, in total. |
| `10` | Ten uses left, in total. The same customer can use several of them. |
| `0` | No uses left. Customers cannot enter the code. `0` does not mean unlimited. |

When does a use count?

- Entering the code in the basket does not use it. Seeing the lower price does not use it either.
- The use is counted when the order completes.
- Because entering a code does not use it, two customers can both have the last use of a code in their basket. The first order to complete gets the use. The other order still completes with the discount. Ekom then flags that order for review ("Coupon could not be claimed") and writes an alert in the order's activity log, so your staff can decide what to do. See [Order reviews](../order-reviews.md).

If the basket changes and no longer fits the offer, Ekom removes the code from the basket. The customer must enter it again.

### What usage limits do not do

Usage limits alone do not give you:

- one use per customer or per account,
- first order only,
- members only, or one customer group only,
- a code that only the person you emailed can use.

These rules need custom work by a developer. A code called `WELCOME` or `MEMBERS` does not limit who can use it. Guest customers and logged-in customers use the same uses of a code.

## Generate many codes at once

Use **Generate Coupons** when you need many different codes for the same discount, for example one code per newsletter subscriber.

1. On the **Coupons** tab, select **Generate Coupons**.
2. Fill in the fields:

   | Field | What it means | Example |
   | --- | --- | --- |
   | Amount | How many different codes to create. This is not the discount amount. The default is `10`. | `50` |
   | Usage limits | The uses each code gets. The default is `1`. | `1` makes every code single-use |
   | Prefix | Optional text at the start of each code. Ekom adds a hyphen after it. | `SPRING` gives codes such as `spring-k7rm2xpa` |
   | Random length | How many random characters come after the prefix. The default is `8`. | `8` |
   | Character set | Which characters the random part uses: **Uppercase letters and numbers**, **Numbers only** or **Letters only**. | **Uppercase letters and numbers** |

3. Select **Generate**. The message "Generated N coupon codes." tells you how many were created.

Enter the prefix without a hyphen: `SPRING`, not `SPRING-`.

Ekom skips a random code that already exists and tries again, up to 10 tries per code you asked for. With a short random length, it can create fewer codes than you asked for. Always check the number in the message.

Generated codes are saved in lower case. The random part leaves out characters that are easy to confuse: the letters I and O, and the numbers 0 and 1.

**Example:** Amount `50` and Usage limits `1` give 50 different codes, each with one use. Using one code does not use up another code.

Sending a code to one person does not tie the code to that person. Anyone who has the code can use it.

## Find, export and delete codes

- **Search:** type in the search box above the list to find codes. The list shows 10 codes per page.
- **Export:** select **Export** to download a CSV file with every code of this discount, its uses left and the date it was created.
- **Delete:** select **Delete** on a row to remove that code. The code is removed at once, without a confirmation step.

## Usage limits are not discount stock

Discount stock is a different limit from **Usage limits**. It is not a field on the order discount: a developer sets it up and changes it from code. It can stop an order at checkout even when the code still has uses left.

- **Usage limits** counts completed orders that used one code.
- **Discount stock** is a separate count for a whole discount, or for one code of a discount. Changing **Usage limits** does not change discount stock.

Do not use **Usage limits** as a count of products or as a shared pool for all codes of a discount. Each code has its own uses. If your campaign needs a limit on the whole discount, ask your developer about discount stock. See [Discount stock](../stock.md#discount-stock).

## Test before you send out codes

1. Enter the code on a basket with campaign products. Then try it on a basket with only other products.
2. Test products that are already on sale, to check your **Stackable** choice.
3. Test another store, and any quantity or basket amount you set.
4. Use a separate test code to complete a test order. Check that **Usage limits** went down by 1. Do not spend real single-use codes on tests.
5. Test a code with `0` uses left. Customers must not be able to enter it.
6. If your shop has guest and logged-in checkout, test both. Both use the same uses of the code.
7. For generated codes, test two different codes. Each one has its own uses.

## Related pages

- [Order discounts](order-discounts.md): the offer that a code switches on.
- [Configure discounts in Umbraco](editors.md): choose the right kind of discount.
- [Order reviews](../order-reviews.md): orders flagged because a code ran out.
- [Discount APIs and behavior](developers.md#apply-and-remove-coupons): apply codes from code, and add rules such as "once per customer", for developers.

[Back to Guides](../README.md)
