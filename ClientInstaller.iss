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

[Dirs]
Name: "{commonappdata}\SystemMonitorService"; Permissions: users-modify

[Files]
Source: "publish_monitor\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
; Step 1: Install the service
Filename: "{sys}\sc.exe"; Parameters: "create SystemMonitorService binPath= ""{app}\SystemMonitorService.exe"" start= auto obj= LocalSystem displayname= ""System Monitor Service"""; Flags: runhidden
; Step 1b: Set service description
Filename: "{sys}\sc.exe"; Parameters: "description SystemMonitorService ""Collects system performance metrics and executes remote administrative commands."""; Flags: runhidden
; Step 1c: Configure failure recovery (Restart after 1 minute)
Filename: "{sys}\sc.exe"; Parameters: "failure SystemMonitorService reset= 86400 actions= restart/60000/restart/60000/restart/60000"; Flags: runhidden

; Step 2: Open configuration file in Notepad
Filename: "notepad.exe"; Parameters: "{app}\appsettings.json"; Description: "Edit configuration (Enter server IP address)"; Flags: shellexec waituntilterminated postinstall

; Step 3: Start the service
Filename: "{sys}\sc.exe"; Parameters: "start SystemMonitorService"; Description: "Start the monitoring service"; Flags: runhidden postinstall

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop SystemMonitorService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete SystemMonitorService"; Flags: runhidden