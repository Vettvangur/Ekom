# Changelog

## Unreleased

- Add a public durable capture service and mutually exclusive station trigger modes for post-warehouse printing.
- Reuse OncePerOrder identities, immutable snapshots and durable processing for explicit-capture retries.

- Resolve uncertain jobs with audited operator confirmation or an idempotent replacement.
- Preserve operator resolutions when delayed agent reports arrive.
- Apply configured retention to confirmed jobs.

- Keep the printer minimum Ekom.Core dependency at 0.2.352 independently of core releases and reject NuGet version substitution during publication.

## [1.0.0](https://github.com/Vettvangur/Ekom/compare/Ekom.Printer-v0.1.0-beta.3...Ekom.Printer-v1.0.0) (2026-11-06)


### Features

* **printer:** resolve uncertain jobs and clarify connection diagnostics ([#1095](https://github.com/Vettvangur/Ekom/issues/1095)) ([ba8ba9a](https://github.com/Vettvangur/Ekom/commit/ba8ba9a951a51a85fff8a7c68113912ff4f87dfb))

## [0.1.0-beta.3](https://github.com/Vettvangur/Ekom/compare/Ekom.Printer-v0.1.0-beta.2...Ekom.Printer-v0.1.0-beta.3) (2026-11-06)


### Bug Fixes

* **printer:** decouple published core dependency from core release version ([#1084](https://github.com/Vettvangur/Ekom/issues/1084)) ([f244d18](https://github.com/Vettvangur/Ekom/commit/f244d187b083c681586c968fadd63c8df5b505c2))

## [0.1.0-beta.2](https://github.com/Vettvangur/Ekom/compare/Ekom.Printer-v0.1.0-beta.1...Ekom.Printer-v0.1.0-beta.2) (2026-11-06)


### Bug Fixes

* **printer:** declare required SQL Server columns non-nullable ([#1080](https://github.com/Vettvangur/Ekom/issues/1080)) ([552c3c7](https://github.com/Vettvangur/Ekom/commit/552c3c7a9133ac3ef3e1c9123406c2c245f0e680))
* **printer:** release the NuGet package independently of Ekom core ([#1082](https://github.com/Vettvangur/Ekom/issues/1082)) ([169f476](https://github.com/Vettvangur/Ekom/commit/169f476ab0e42f4c0297d0da8debf6a15bb7ee53))

## Changelog

## Unreleased

- Fix SQL Server first-install printer migrations by explicitly mapping required string columns, including primary keys, as NOT NULL.

- Document agent SmartScreen warnings, checksum verification, company IT approval and signing limitations.

- Group printer source projects under Ekom/Printing and a shared Printing solution folder.

- Document station token and TokenSha256 generation on macOS and Windows, including where each value is configured.

- Capture eligible order statuses in the order transaction and recover pending jobs after restarts.
- Add package-owned version 3 capture migration and retry failed custom snapshot creation in the background.

- Start optional Ekom.Printer package with configurable HTML picking-slip queue.
- Add package-owned migrations, snapshot/renderer extension points and retention cleanup.
- Add versioned HTTPS station API with hashed credentials, atomic claims, expiry review and replay-safe acknowledgements.
- Add package-owned version 2 delivery/health migration and heartbeat/retention processing.
- Add agent transport and durable submission journal, Windows service and setup application, restricted HTML renderer, and NSIS installer.
- Validate installation, setup communication, reinstall and uninstall in Windows GitHub Actions.
- Add separate NuGet and installer release workflows and staging acceptance instructions.
