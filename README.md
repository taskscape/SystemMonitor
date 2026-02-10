# SystemMonitor Suite

A lightweight, standalone system monitoring solution consisting of a Windows Service (Collector), a Client Agent (Monitor), and a Mobile App.

## Standalone Architecture

The system has been migrated to a **standalone SQLite architecture**. It no longer requires Docker, PostgreSQL, or RabbitMQ. Data is stored in local SQLite databases, making it extremely easy to deploy.

### Key Features:
- **Zero External Dependencies:** No database or message queue servers required.
- **SQLite Storage:** High-performance local storage for both Collector and Monitor.
- **Automated Installers:** Simple `.exe` installers that configure everything automatically.
- **Self-Contained:** Bundles the .NET runtime, so no pre-installation is needed.

## Deployment

The easiest way to install the system is using the provided installers:

1.  **SystemMonitorFullSetup.exe:** Installs both the Server (Collector) and the Client (Agent) on a single machine.
2.  **SystemMonitorServerSetup.exe:** Installs only the Collector (API and Dashboard).
3.  **SystemMonitorClientSetup.exe:** Installs only the Agent on machines you wish to monitor.

### Automatic Configuration
- **Database location:** `%ProgramData%\SystemMonitor\`
- **Logs:** `%ProgramData%\SystemMonitor\logs\`
- **HTTPS Port:** `5101` (Traffic is encrypted by default)

---

## SystemCollectorService (Server)

A centralized service that receives metrics from agents, stores them in SQLite, and provides a modern web dashboard.

### Web UI & API
- **Dashboard:** `https://localhost:5101/`
- **Metrics API:** `POST /api/v1/metrics`
- **Machine List:** `GET /api/v1/machines`

### Configuration (`appsettings.json`)
- `CollectorSettings:ConnectionString` - SQLite connection string (default: `Data Source=system_monitor.db`).
- `CollectorSettings:RetentionDays` - How many days of history to keep (default: `7`).

---

## SystemMonitorService (Client Agent)

A background service that samples CPU, RAM, disk, and process usage, stores them locally if the server is offline, and pushes them to the collector.

### Configuration (`appsettings.json`)
- `MonitorSettings:CollectorEndpoint` - Address of your collector (e.g., `https://192.168.1.10:5101/api/v1/metrics`).
- `MonitorSettings:TrustAllCertificates` - Set to `true` if using self-signed development certificates.

---

## SystemMonitorMobile (Mobile App)

A MAUI application to monitor your fleet from any device.

### Configuration (`appsettings.json`)
- `CollectorSettings:BaseUrl` - URL of the collector (e.g., `https://192.168.1.10:5101`).
