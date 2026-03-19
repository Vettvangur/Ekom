<h1 align="center">
Ekom
 
[![Nuget](https://img.shields.io/nuget/vpre/Ekom.U10?color=ed0f0f)](https://www.nuget.org/packages/Ekom.U10/)
[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Klaviyo?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Klaviyo/)
[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Algolia?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Algolia/)
[![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE)
[![Publish Ekom.Klaviyo](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-klaviyo.yml/badge.svg?branch=Ekom)](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-klaviyo.yml)
[![Publish Ekom.Algolia](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml/badge.svg?branch=Ekom)](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml)

</h1>

<h2 align="center">
Open Source Ecommerce package for Umbraco
</h2>

Supports Umbraco version 10+

Ekom is a versatile and fully customizable eCommerce solution that is free to use forever. This package has been built with ASP NET Core, focusing on performance and security, and is compatible with Umbraco versions 10 and above.

## Installation
Install the appropriate Umbraco versioned package to your solution (f.x. Ekom.U10)
Install the Ekom.Web package into your sites main project (contains wwwroot)

**NuGet:** [https://www.nuget.org/packages/Ekom.U10](https://www.nuget.org/packages/Ekom.U10)

`dotnet add package Ekom.U10`

`PM> Install-Package Ekom.U10`

##### Ekom.Web

`dotnet add package Ekom.Web`

`PM> Install-Package Ekom.Web`

## Some of the key featured of Ekom includes:

- 100% free and open source forever.
- Support for both B2B (Business to Business) and B2C (Business to Customer) transactions
- Headless capabilities, allowing for more flexibility in frontend design and development
- Multilingual and multicurrency support, making it suitable for international businesses
- A powerful order management system
- Out-of-the-box support for multiple payment providers, along with the ability to plug in additional providers as needed
- Flexible shipping provider configurations, including the ability to connect with external services
- The capability to set up flexible discounts
- Support for complex variants, a crucial feature for eCommerce platforms
- Integration support with external systems like Microsoft Dynamics Business Central, Dynamics AX, DK, Salesforce, and others
- Advanced inventory management tools
- Built with ASP NET Core with focus on performance and security
- The ability to be extended as per your business requirements
- And many more features....

## Appsettings configuration

All Ekom settings live under the `Ekom` section in `appsettings.json`.

```json
"Ekom": {
  "PerStoreStock": false,
  "ExamineSearchIndex": "ExternalIndex",
  "ShareBasket": false,
  "BasketCookieLifetime": 1,
  "CustomImage": "images",
  "ReservationTimeout": 30,
  "CategoryRootLevel": 3,
  "VatCalcRounding": "AwayFromZero",
  "VatRoundingScope": "PerUnit",
  "VatIncludedPerUnitPolicy": "PreserveStickerGross",
  "ApplyVatOnShipping": true,
  "UserBasket": false,
  "DisableStock": false,
  "AbsoluteUrls": true,
  "DefaultProductOrderBy": "DateDesc",
  "GlobalCatalog": false,
  "EmailNotifications": "orders@example.com",
  "CustomerData": false,
  "Manager": {
    "SectionAccessGroup": "ekom,commerce-admins",
    "StoreGroupPermissions": {
      "store1": [ "group-a", "group-b" ]
    }
  },
  "Headless": {
    "ReValidateApis": [
      { "Store": "store1", "Url": "https://example.com/api/revalidate", "Secret": "secret" }
    ]
  },
  "Payments": {
    "valitor": {
      "merchantId": "1",
      "verificationCode": "xxxxx",
      "merchantName": "",
      "paymentPageUrl": "https://paymentweb.uat.valitor.is/"
    }
  }
}
```

- `PerStoreStock` (bool, default `false`): Use per-store stock cache instead of product/variant stock.
- `ExamineSearchIndex` (string, default `ExternalIndex`): Examine index name used for search.
- `ShareBasket` (bool, default `false`): Share baskets between stores; requires same currencies across stores.
- `BasketCookieLifetime` (number, days, default `1`): Order cookie lifespan in days.
- `CustomImage` (string, default `images`): Media folder alias for product images.
- `ReservationTimeout` (number, minutes, default `30`): Checkout reservation timeout in minutes.
- `CategoryRootLevel` (int, default `3`): Minimum Umbraco level for categories.
- `VatCalcRounding` (Rounding enum, default `AwayFromZero`): `None`, `RoundDown`, `RoundUp`, `RoundToEven`, `AwayFromZero`.
- `VatRoundingScope` (VatRoundingScope enum, default `PerUnit`): `PerUnit`, `PerTotal`.
- `VatIncludedPerUnitPolicy` (VatIncludedPerUnitPolicy enum, default `PreserveStickerGross`): `PreserveStickerGross`, `LineLevelVat`.
- `ApplyVatOnShipping` (bool, default `false`): Apply VAT to shipping costs.
- `UserBasket` (bool, default `false`): Single basket per member stored on the member "orderId".
- `DisableStock` (bool, default `false`): Disable stock checks.
- `AbsoluteUrls` (bool, default `false`): Force backoffice URLs to be absolute for multi-site setups.
- `DefaultProductOrderBy` (OrderBy enum, default `DateDesc`): See `Ekom.Utilities.Enums.OrderBy` values for options.
- `GlobalCatalog` (bool, default `false`): If product not found in current store, search other stores.
- `EmailNotifications` (string, optional): Override Umbraco email for `MailService` notifications.
- `CustomerData` (bool, default `false`): Store checkout customer data in `ekmCustomerData` table.
- `Manager:SectionAccessGroup` (CSV string): Allowed backoffice groups for the Ekom section.
- `Manager:StoreGroupPermissions` (object): Store alias to allowed group list mapping.
- `SectionAccessRules` (CSV string, legacy): Backwards-compatible alias for `Manager:SectionAccessGroup`.
- `Headless:ReValidateApis` (list): Items with `Store`, `Url`, `Secret` for headless revalidation.
- `Payments` (object): Provider-specific configuration used by payment providers.

## Plugins
- https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Klaviyo
- https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Algolia

## Documentation

[Link to documentation](https://vettvangur.gitbook.io/ekom/)

## Contributing

We use squash merges and Conventional Commit style PR titles so release-please can generate release PRs.

If you must use merge commits, every individual commit message still has to be Conventional Commits.

Example PR titles:

```text
feat: add vat rounding settings to docs
fix: handle null payment provider in checkout
chore: update dependencies
```
