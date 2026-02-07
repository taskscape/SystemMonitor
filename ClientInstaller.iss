[Setup]
AppName=System Monitor Client
AppVersion=1.1
DefaultDirName={autopf}\SystemMonitorClient
DefaultGroupName=System Monitor
OutputDir=.
OutputBaseFilename=SystemMonitorClientSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "publish_monitor\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; Create service
Filename: "{sys}\sc.exe"; Parameters: "create SystemMonitorService binPath= ""{app}\SystemMonitorService.exe"" start= auto obj= LocalSystem"; Flags: runhidden

; Open config for IP address setup
Filename: "notepad.exe"; Parameters: "{app}\appsettings.json"; Description: "Configure Server IP address"; Flags: shellexec waituntilterminated postinstall

; Start service
Filename: "{sys}\sc.exe"; Parameters: "start SystemMonitorService"; Description: "Start monitoring service"; Flags: runhidden postinstall

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop SystemMonitorService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete SystemMonitorService"; Flags: runhidden