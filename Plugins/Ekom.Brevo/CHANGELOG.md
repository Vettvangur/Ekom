# Changelog

## Unreleased

- Persist transactional email in the Umbraco database with cross-instance claims, restart recovery, store-scoped idempotency, durable rate-limit retries and retention.
- Add authenticated email delivery webhooks with duplicate handling, early-response correlation and per-recipient event history.
- Add delivery/order lookup and explicit retry of definitive failures through IBrevoDeliveryService.
- Keep email-only sending and Umbraco 17, 18 and 19 support.

## [1.1.0](https://github.com/Vettvangur/Ekom/compare/Ekom.Brevo-v1.0.0...Ekom.Brevo-v1.1.0) (2026-11-27)


### Miscellaneous Chores

* **Ekom.Brevo:** Synchronize Ekom.Brevo versions

## 1.0.0 (2026-11-27)


### Features

* **brevo:** add integration and automated NuGet releases ([#1278](https://github.com/Vettvangur/Ekom/issues/1278)) ([58bd95f](https://github.com/Vettvangur/Ekom/commit/58bd95f143a8079fe6e241a88200ec118f84cda4))

## 1.0.0

- Add Brevo integration for Umbraco 17, 18, and 19.
- Add contact synchronization, email subscriptions, double opt-in, and list management.
- Add completed-checkout order synchronization and explicit email consent handling.
- Add queued transactional email with templates, HTML/text, CC/BCC, and attachments.
- Add separate bounded queues, delivery results, store overrides, and conservative retries.
