[Setup]
AppName=System Monitor Suite
AppVersion=1.1
DefaultDirName={autopf}\SystemMonitorSuite
DefaultGroupName=System Monitor
OutputDir=.
OutputBaseFilename=SystemMonitorFullSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Types]
Name: "full"; Description: "Full installation (Server and Client)"
Name: "server_only"; Description: "Server only"
Name: "client_only"; Description: "Client only"
Name: "custom"; Description: "Custom installation"; Flags: iscustom

[Components]
Name: "server"; Description: "System Monitor Server (Collector)"; Types: full server_only custom
Name: "client"; Description: "System Monitor Client (Agent)"; Types: full client_only custom

[Dirs]
Name: "{commonappdata}\SystemMonitor"; Permissions: users-modify
Name: "{commonappdata}\SystemMonitorService"; Permissions: users-modify

[Files]
; Server Files
Source: "publish_collector\*"; DestDir: "{app}\Server"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: server
; Client Files
Source: "publish_monitor\*"; DestDir: "{app}\Client"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: client

[Run]
; --- Server Setup ---
; Firewall rule for Server (HTTPS port 5101)
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""System Monitor Collector"" dir=in action=allow protocol=TCP localport=5101"; Flags: runhidden; Components: server
; Create and Start Server Service
Filename: "{sys}\sc.exe"; Parameters: "create SystemCollectorService binPath= ""{app}\Server\SystemCollectorService.exe"" start= auto obj= LocalSystem"; Flags: runhidden; Components: server
Filename: "{sys}\sc.exe"; Parameters: "start SystemCollectorService"; Flags: runhidden; Components: server
; Open dashboard
Filename: "https://localhost:5101"; Flags: shellexec nowait postinstall skipifsilent; Description: "Open Server Dashboard"; Components: server

; --- Client Setup ---
; Create Client Service
Filename: "{sys}\sc.exe"; Parameters: "create SystemMonitorService binPath= ""{app}\Client\SystemMonitorService.exe"" start= auto obj= LocalSystem"; Flags: runhidden; Components: client
; Edit configuration
Filename: "notepad.exe"; Parameters: "{app}\Client\appsettings.json"; Description: "Configure Client (Set Server IP)"; Flags: shellexec waituntilterminated postinstall; Components: client
; Start Client Service
Filename: "{sys}\sc.exe"; Parameters: "start SystemMonitorService"; Description: "Start Client Monitoring Service"; Flags: runhidden postinstall; Components: client

[UninstallRun]
; Cleanup Server
Filename: "{sys}\sc.exe"; Parameters: "stop SystemCollectorService"; Flags: runhidden; Components: server
Filename: "{sys}\sc.exe"; Parameters: "delete SystemCollectorService"; Flags: runhidden; Components: server
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""System Monitor Collector"""; Flags: runhidden; Components: server
; Cleanup Client
Filename: "{sys}\sc.exe"; Parameters: "stop SystemMonitorService"; Flags: runhidden; Components: client
Filename: "{sys}\sc.exe"; Parameters: "delete SystemMonitorService"; Flags: runhidden; Components: client
