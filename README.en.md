# AirPageSystem

Current release: **v0.0.5**. Self-contained Windows, Linux and macOS packages that do not require a preinstalled .NET runtime are available from [Releases](https://github.com/sunkejava/AirPageSystem/releases).

The built-in **3x-ui Proxy Monitor** panel reads a local 3x-ui SQLite database in read-only mode and displays 3x-ui/Xray process state, versions, uptime, upload/download totals, inbound/client/IP counts, active connections, host addresses, and high-traffic inbounds. Set `ThreeXUi__DatabasePath` when the database is not in a standard location. Docker users should mount `/etc/x-ui/x-ui.db` read-only into the container.

AirPageSystem is a **.NET 10 + Vue 3** snapshot builder and scheduler for AirPage e-ink devices. It collects market data, server telemetry, or custom HTTP JSON, renders 528×792 PNG previews and firmware-compatible 2-bit BMP files, and pushes them to AirPage.

## Highlights

- Cookie-based login, users, roles, menu permissions, tenant data isolation, and a Vue admin UI.
- Built-in Live Market dashboard with A-share indices, breadth, limits, and volatile stocks.
- Built-in Server Status dashboard with memory, disk, uptime, traffic, and process rankings.
- Declarative HTTP JSON mappings plus safe JSON drawing presets for quotes, badges, boarding passes, free layouts, and uploaded images.
- Five-field Cron schedules with per-job time zones and configurable exponential-backoff retry policies.
- Encrypted device IDs; credentials are never returned by list APIs or logs.
- Exact four-color, 2-bit, bottom-up BMP encoding and the 512 KiB limit.
- SQLite, Docker, and GitHub Actions.

## Start

~~~bash
docker compose up -d --build
~~~

Open http://localhost:5088. On first startup, sign in as `admin` with the one-time random password printed only to the local startup log, or preset it using `BootstrapAdmin__Password`. Runtime state is stored in data/ and excluded from Git.

## Security

Only trusted AirPage HTTPS origins are accepted. Private-network custom sources are disabled by default to reduce SSRF risk. Keep device URLs, tokens, production settings, the database, and Data Protection keys out of Git. Authentication and RBAC are built in; configure TLS and a secret store before exposing the UI publicly.

See [README.md](README.md) for full setup, Cron examples, custom mappings, APIs, and limitations.
