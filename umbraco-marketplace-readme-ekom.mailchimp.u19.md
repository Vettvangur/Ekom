# Ekom Mailchimp

Mailchimp for Ekom stores. It supports audience tags and subscriptions, tracks completed purchases, and queues Mailchimp Transactional (Mandrill) messages.

## Install

Install the package that matches your Umbraco version. Ekom must already be installed on the site.

| Umbraco | Package |
| --- | --- |
| 17 | `Ekom.Mailchimp` |
| 18 | `Ekom.Mailchimp.U18` |
| 19 (release candidate) | `Ekom.Mailchimp.U19` |

```bash
dotnet add package Ekom.Mailchimp
```

Then register the services at startup:

```csharp
builder.Services.AddMailchimp();
```

## Documentation

- [Configuration reference](https://github.com/Vettvangur/Ekom/tree/Ekom/Plugins/Ekom.Mailchimp)
- [Ekom integrations](https://vettvangur.gitbook.io/ekom/integrations)
- [Ekom documentation](https://vettvangur.gitbook.io/ekom)

## Help

Report bugs and ask questions in [GitHub issues](https://github.com/Vettvangur/Ekom/issues).
