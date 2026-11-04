# Changelog

## Unreleased

- Capture eligible order statuses in the order transaction and recover pending jobs after restarts.
- Add package-owned version 3 capture migration and retry failed custom snapshot creation in the background.

- Start optional Ekom.Printer package with configurable HTML picking-slip queue.
- Add package-owned migrations, snapshot/renderer extension points and retention cleanup.
- Add versioned HTTPS station API with hashed credentials, atomic claims, expiry review and replay-safe acknowledgements.
- Add package-owned version 2 delivery/health migration and heartbeat/retention processing.
- Add agent transport and durable submission journal. Windows renderer, host and installer remain planned.
