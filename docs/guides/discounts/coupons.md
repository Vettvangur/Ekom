# Coupons for editors

[Editor overview](editors.md) | [Order discounts](order-discounts.md) | [Developer guide](developers.md)

A coupon code activates an **order discount**. The code does not define the percentage, eligible items, or quantity rule: those come from the discount it belongs to.

Use coupons for a shared campaign such as SPRING10, individually issued single-use codes, or offers that customers must explicitly activate.

## Configure the offer first

1. Create an [Order Discount](order-discounts.md#create-the-discount) under the intended store.
2. Set Type, Discount, Discount Items, exclusions, and any quantity rule.
3. Leave **Global Discount off** for an ordinary code-based offer.
4. Confirm the store/currency values and save the discount before adding codes. If the editor asks you to refresh after saving, do so.
5. Open the discount's **Coupons** tab.

Automatic global discounts should be couponless. Adding codes to a global discount prevents automatic application; Global Discount also broadens reward targeting. Use separate discounts when you need separate automatic and code-based offers.

## Add a shared code

Choose **Add Code** (or the equivalent action in your editor), then fill in:

| Field | Meaning | Example |
| --- | --- | --- |
| Coupon Code | The code customers enter. | `SPRING10` |
| Usage limits | How many uses remain for this code across all customers. | `100` for 100 uses in total. |

Confirm with **Add**. Save and publish the underlying discount content as needed before testing on the storefront. Code records are managed through the coupon editor; publishing the discount is not a substitute for adding a code.

### Example: 10% off selected items for the first 100 code uses

**Goal:** a shared campaign offering 10% off selected items, with 100 uses available in total.

| Setting | Value |
| --- | --- |
| Order discount Type | Percentage |
| Discount | `10` |
| Discount Items | The campaign products/category |
| Global Discount | Off |
| Stackable | Off, unless an additional reduction on sale prices is intended |
| Quantity Discount Mode | None |
| Coupon Code | `SPRING10` |
| Usage limits | `100` |

**Expected result:** entering the code on a qualifying basket offers 10% off the selected eligible items, unless a better discount or other restriction prevents it. Other items are not discounted by this non-global offer. Under the standard completion flow, a completed order using the order-level code consumes one use.

This is **not** 100 uses per customer or a guarantee that exactly 100 distinct people receive it. For a hard campaign cap during concurrent checkouts or a custom checkout integration, ask your developer to verify the end-to-end validation and usage workflow.

## Understand usage limits

> **Usage limits belong to the coupon code, not to a customer.** Ekom does not use customer ID, email, username, or login state to make this a per-customer allowance. A limit of 1 means single-use overall, not once per customer.

The value shown is the code's **remaining uses**. It decreases when the code is marked used. For example, a code starting with 10 uses has 9 left after one use is recorded.

| Remaining uses | Meaning |
| --- | --- |
| `1` | The code has one use left in total. Another customer does not receive a separate allowance. |
| `10` | The code has ten uses left in total. The counter itself does not prevent one customer from using it on multiple orders. |
| `0` | The code is exhausted and cannot be newly applied. **Zero does not mean unlimited.** |

Entering a code in a basket does not itself consume a use. In the standard checkout flow, completion marks the order-level coupon used. Merely displaying or previewing the discount is not a recorded use. Custom checkout systems can have their own integration workflow; check with your developer before relying on it.

### What usage limits do not enforce

Usage limits alone do not mean:

- Once per customer or logged-in account.
- First order only.
- Members-only or a particular customer group.
- A code can only be used by the person it was emailed to.

These restrictions need additional custom validation. A code named WELCOME or MEMBERS does not enforce them. Guest and logged-in checkouts draw from the same code allowance; the site can impose other restrictions separately.

## Generate individual codes

Use **Generate Coupons** when you need several different codes for the same discount. The modern editor provides:

| Field | Meaning | Example |
| --- | --- | --- |
| Amount | Number of different codes to generate, not the discount value. | `50` generates 50 codes. |
| Usage limits | Remaining-use allowance assigned to **each** generated code. | `1` makes each code single-use overall. |
| Prefix | Optional text at the start of each code; a hyphen separates it from the random portion. | Enter `SPRING`, not `SPRING-`, for codes such as `spring-abcdefgh`. |
| Random length | Length of the generated random portion. | `8` produces an eight-character random portion. |
| Character set | Characters allowed in the random portion. | Uppercase letters and numbers, numbers only, or letters only. |

Choose **Generate**, then inspect the list and confirm the number actually created; duplicate attempts can reduce the result. Generated codes are stored in lowercase, and coupon input is normalized to lowercase. Labels and generation options can differ between Umbraco versions; use the options available in your editor. Use **Export** where available to download the codes and remaining-use values.

**Example:** Amount = 50 and Usage limits = 1 gives 50 different codes, each with its own one-use allowance. Using one code does not consume another code's usage allowance. Other discount or stock restrictions can still prevent a code from applying.

Sending each code to one person does not bind it to that person. Anyone who has the code could use its allowance unless your site adds recipient validation.

## Usage limits are not discount stock

Coupon usage counts uses of a code. Discount stock is a separate allowance managed through checkout/stock integration, and can block an offer even when the code has remaining uses.

Do not treat Usage limits as a stock quantity, a discounted-unit count, or a shared pool for every code attached to a discount. Master-discount and coupon-specific stock are separate developer-managed mechanisms; they are not the standard Usage limits field. Ask your developer if your campaign requires both types of limit.

## Test before distributing codes

- Apply the code to a basket with eligible items, then to one containing only unrelated items.
- Test existing sale prices and your Stackable choice.
- Test another store and any configured quantity or amount requirements.
- Using a dedicated test code, complete a test order and check that its remaining uses decrease. Do not consume live single-use codes just to test.
- Test a code with no uses left; it must not be newly applied.
- Where your storefront supports both, test guest and logged-in checkout. Login should not be mistaken for a separate usage allowance.
- For generated codes, test two different codes to confirm each has its own usage count.

For API access, custom customer restrictions, and trusted integration behavior, see the [developer guide](developers.md#apply-and-remove-coupons).
