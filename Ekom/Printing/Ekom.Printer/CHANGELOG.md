# Changelog

## Unreleased

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
