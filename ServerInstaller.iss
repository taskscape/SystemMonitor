[Setup]
AppName=System Monitor Server
AppVersion=1.1
DefaultDirName={autopf}\SystemMonitorServer
DefaultGroupName=System Monitor
OutputDir=.
OutputBaseFilename=SystemMonitorServerSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Dirs]
Name: "{commonappdata}\SystemMonitor"; Permissions: users-modify

[Files]
; Copy files from server publication folder
Source: "publish_collector\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; Open port 5100 for HTTP
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""System Monitor Collector HTTP"" dir=in action=allow protocol=TCP localport=5100"; Flags: runhidden

; Register and start the service
Filename: "{sys}\sc.exe"; Parameters: "create SystemCollectorService binPath= ""{app}\SystemCollectorService.exe"" start= auto obj= LocalSystem"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start SystemCollectorService"; Flags: runhidden

; Open browser after installation
Filename: "http://localhost:5100"; Flags: shellexec nowait postinstall skipifsilent; Description: "Open monitoring dashboard"

[UninstallRun]
; Cleanup
Filename: "{sys}\sc.exe"; Parameters: "stop SystemCollectorService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete SystemCollectorService"; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""System Monitor Collector HTTP"""; Flags: runhidden