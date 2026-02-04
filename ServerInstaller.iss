[Setup]
AppName=System Monitor Server
AppVersion=1.0
DefaultDirName={autopf}\SystemMonitorServer
DefaultGroupName=System Monitor
OutputDir=.
OutputBaseFilename=SystemMonitorServerSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Files]
; Copy files from server publication folder
Source: "publish_collector\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; Open port 5100 in Firewall
Filename: "netsh"; Parameters: "advfirewall firewall add rule name=""System Monitor Collector"" dir=in action=allow protocol=TCP localport=5100"; Flags: runhidden

; Register and start the service
Filename: "{sys}\sc.exe"; Parameters: "create SystemCollectorService binPath= ""{app}\SystemCollectorService.exe"" start= auto obj= LocalSystem"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "start SystemCollectorService"; Flags: runhidden

; Open browser after installation
Filename: "http://localhost:5100"; Flags: shellexec nowait postinstall skipifsilent; Description: "Open monitoring dashboard"

[UninstallRun]
; Cleanup
Filename: "{sys}\sc.exe"; Parameters: "stop SystemCollectorService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete SystemCollectorService"; Flags: runhidden
Filename: "netsh"; Parameters: "advfirewall firewall delete rule name=""System Monitor Collector"""; Flags: runhidden
