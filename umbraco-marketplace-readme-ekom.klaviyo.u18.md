# Ekom Klaviyo

Klaviyo for Ekom stores. It sends order events to Klaviyo and keeps the Klaviyo catalog in step with the products in Ekom.

## Install

Install the package that matches your Umbraco version. Ekom must already be installed on the site.

| Umbraco | Package |
| --- | --- |
| 13 and 17 | `Ekom.Klaviyo` |
| 18 | `Ekom.Klaviyo.U18` |
| 19 (release candidate) | `Ekom.Klaviyo.U19` |

```bash
dotnet add package Ekom.Klaviyo
```

Then register the services at startup:

```csharp
builder.Services.AddKlaviyo();
```

## Documentation

- [Configuration reference](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Klaviyo)
- [Ekom integrations](https://vettvangur.gitbook.io/ekom/integrations)
- [Ekom documentation](https://vettvangur.gitbook.io/ekom)

## Help

Report bugs and ask questions in [GitHub issues](https://github.com/Vettvangur/Ekom/issues).
