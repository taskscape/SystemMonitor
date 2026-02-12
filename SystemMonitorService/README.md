# SystemMonitorService

The background agent that collects performance data and sends it to the collector.

## Standalone Features
- **Self-Contained:** No .NET runtime required on the host machine.
- **Dual Storage:** Uses local SQLite buffering to prevent data loss during network outages.
- **Smart Paths:** 
  - **Windows:** Uses `C:\ProgramData\SystemMonitorService\`
  - **Linux:** Uses `~/.systemmonitor-agent/` (allows running without sudo).

## Installation
- **Windows:** Use `SystemMonitorClientSetup.exe`.
- **Linux:** Run `install_agent.sh` or use `sudo apt install ./system-monitor-agent.deb`.

## Configuration (`appsettings.json`)
- `MonitorSettings:CollectorEndpoint`: Point this to your server (e.g., `http://192.168.1.50:5100/api/v1/metrics`).
- `MonitorSettings:RetentionDays`: Local history size (default 7 days).
