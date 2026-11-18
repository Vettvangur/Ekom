# Ekom Algolia

Algolia search for Ekom stores. It indexes products, categories and Umbraco content in the background, adds search services for all three, and sends Algolia Insights events for views, add-to-cart, checkout and purchases.

## Install

Install the package that matches your Umbraco version. Ekom must already be installed on the site.

| Umbraco | Package |
| --- | --- |
| 13 and 17 | `Ekom.Algolia` |
| 18 | `Ekom.Algolia.U18` |
| 19 (release candidate) | `Ekom.Algolia.U19` |

```bash
dotnet add package Ekom.Algolia
```

Then register the services at startup:

```csharp
builder.Services.AddAlgolia();
```

## Documentation

- [Configuration reference](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Algolia)
- [Ekom integrations](https://vettvangur.gitbook.io/ekom/integrations)
- [Ekom documentation](https://vettvangur.gitbook.io/ekom)

## Help

Report bugs and ask questions in [GitHub issues](https://github.com/Vettvangur/Ekom/issues).
