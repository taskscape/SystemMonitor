# SystemMonitor

A lightweight, standalone system monitoring solution consisting of a Windows/Linux Service (Collector), a Client Agent (Monitor), and a Mobile App.

## 🚀 Key Features

- **Standalone Architecture:** Migrated to SQLite. No more Docker, PostgreSQL, or RabbitMQ required.
- **Zero Dependencies:** Bundles the .NET 10 runtime (Self-Contained), so no pre-installation is needed on target machines.
- **Cross-Platform:** Full support for Windows and Linux.
- **HTTP Mode:** Defaulting to HTTP on port `5100` to avoid certificate issues in local environments.
- **Automated Installers:** Professional `.exe` installers for Windows and `.deb` packaging for Linux.

---

## 💻 Deployment

### Windows
The easiest way to install is using the provided installers:
1. **SystemMonitorFullSetup.exe:** Installs both Server and Client.
2. **SystemMonitorServerSetup.exe:** Installs only the Collector (API & Dashboard).
3. **SystemMonitorClientSetup.exe:** Installs only the Agent.

*Default Port:* `5100`
*Data Path:* `%ProgramData%\SystemMonitor\`

### Linux
You can install the agent or server using the provided scripts:
- **One-Liner Install (Agent):**
  `curl -sSL https://raw.githubusercontent.com/taskscape/SystemMonitor/main/install_agent.sh | sudo bash`
- **Manual Setup:** Use `setup_linux.sh` for interactive installation or `create_deb.sh` to generate native Debian packages.

*Data Path:* `~/.systemmonitor/` (to avoid permission issues).

---

## 🛠 Component Overview

### SystemCollectorService (Server)
The central hub that receives metrics and hosts the web dashboard.
- **Dashboard:** `http://<server-ip>:5100/`
- **Storage:** SQLite database with automatic 7-day retention cleanup.

### SystemMonitorService (Agent)
A background service that samples CPU, RAM, Disk, and Process usage.
- **Local Buffering:** Stores metrics locally if the server is unreachable.
- **Auto-Config:** Automatically configures permissions and firewall rules during installation.

### SystemMonitorMobile (App)
MAUI application to monitor your fleet from any device. Update `appsettings.json` with your Server IP.
