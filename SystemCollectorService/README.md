# SystemCollectorService

Solution: SystemMonitor (Server)

## Overview

The `SystemCollectorService` is the central hub of the SystemMonitor suite. It receives performance metrics from agents, stores them in a local SQLite database, and provides a web dashboard.

## Standalone Architecture

This service has been optimized for simplicity and ease of deployment:
- **Storage:** Uses SQLite for all data (no external database server required).
- **Processing:** Metrics are stored synchronously upon receipt (no message queue required).
- **Deployment:** Bundles the .NET runtime for zero-dependency execution.

## Configuration

File: `appsettings.json`

- `CollectorSettings:ConnectionString`: SQLite connection string (e.g., `Data Source=system_monitor.db`).
- `CollectorSettings:ListenUrl`: HTTPS listen URL (default `https://0.0.0.0:5101`).
- `CollectorSettings:RetentionDays`: Automatic cleanup window (default 7 days).

## Automatic Setup

The recommended way to deploy is using the **SystemMonitorServerSetup.exe** installer, which automatically:
1. Configures the database in `%ProgramData%\SystemMonitor\`.
2. Sets up the Windows Service with the correct permissions.
3. Opens firewall port `5101` for incoming metrics.

## Manual Deployment

```bash
dotnet publish -c Release -r win-x64 --self-contained
```
Install as a Windows service:
```powershell
sc.exe create SystemCollectorService binPath= "C:\Path\To\SystemCollectorService.exe" start= auto obj= LocalSystem
```