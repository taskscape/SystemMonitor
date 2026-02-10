# SystemCollectorService

The central hub of the SystemMonitor suite. It receives performance metrics from agents and provides a web dashboard.

## Standalone Architecture
- **Storage:** SQLite (no external database server required).
- **Processing:** Direct synchronous storage (no RabbitMQ needed).
- **Deployment:** Bundled .NET runtime for zero-dependency execution.
- **Default URL:** `http://0.0.0.0:5100`

## Linux Support
The collector can now run on Linux. It automatically detects the OS and:
1. Uses preprocessor directives to skip Windows-specific service logic.
2. Stores the database in `~/.systemmonitor/` to comply with Linux permission standards.

## Configuration (`appsettings.json`)
- `CollectorSettings:ConnectionString`: SQLite connection string.
- `CollectorSettings:ListenUrl`: HTTP address (default `http://0.0.0.0:5100`).
- `CollectorSettings:RetentionDays`: Cleanup window (default 7 days).
