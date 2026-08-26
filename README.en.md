# AirPageSystem

Current release: **v0.0.4**. Self-contained Windows, Linux and macOS packages that do not require a preinstalled .NET runtime are available from [Releases](https://github.com/sunkejava/AirPageSystem/releases).

The built-in **3x-ui Proxy Monitor** panel reads a local 3x-ui SQLite database in read-only mode and displays 3x-ui/Xray process state, versions, uptime, upload/download totals, inbound/client/IP counts, active connections, host addresses, and high-traffic inbounds. Set `ThreeXUi__DatabasePath` when the database is not in a standard location. Docker users should mount `/etc/x-ui/x-ui.db` read-only into the container.

AirPageSystem is a **.NET 10 + Vue 3** snapshot builder and scheduler for AirPage e-ink devices. It collects market data, server telemetry, or custom HTTP JSON, renders 528×792 PNG previews and firmware-compatible 2-bit BMP files, and pushes them to AirPage.

## Highlights

- Vue admin UI for templates, sources, schedules, devices, previews, and history.
- Built-in Live Market dashboard with A-share indices, breadth, limits, and volatile stocks.
- Built-in Server Status dashboard with memory, disk, uptime, traffic, and process rankings.
- Declarative custom JSON mappings.
- Five-field Cron schedules with per-job time zones.
- Encrypted device IDs; credentials are never returned by list APIs or logs.
- Exact four-color, 2-bit, bottom-up BMP encoding and the 512 KiB limit.
- SQLite, Docker, and GitHub Actions.

## Start

~~~bash
docker compose up -d --build
~~~

Open http://localhost:5088, add a device, preview a template, then push or schedule it. Runtime state is stored in data/ and excluded from Git.

## Security

Only trusted AirPage HTTPS origins are accepted. Private-network custom sources are disabled by default to reduce SSRF risk. Keep device URLs, tokens, production settings, the database, and Data Protection keys out of Git. Add authentication, RBAC, TLS, and a secret store before exposing the UI publicly.

See [README.md](README.md) for full setup, Cron examples, custom mappings, APIs, and limitations.
