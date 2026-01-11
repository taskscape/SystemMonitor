[Setup]
AppName=SystemMonitorService
AppVersion=1.0.0
DefaultDirName={pf}\SystemMonitorService
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputBaseFilename=SystemMonitorServiceInstaller

[Run]
Filename: "{sys}\sc.exe"; Parameters: "stop SystemMonitorService"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "create SystemMonitorService binPath= ""C:\Projects\SystemMonitor\SystemMonitorService\bin\Release\net10.0-windows\SystemMonitorService.exe"" start= auto obj= LocalSystem"; Flags: runhidden waituntilterminated
Filename: "{sys}\sc.exe"; Parameters: "start SystemMonitorService"; Flags: runhidden waituntilterminated
