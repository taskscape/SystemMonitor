[Setup]
AppName=System Monitor Client
AppVersion=1.0
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
; Krok 1: Instalacja usługi
Filename: "{sys}\sc.exe"; Parameters: "create SystemMonitorService binPath= ""{app}\SystemMonitorService.exe"" start= auto obj= LocalSystem displayname= ""System Monitor Service"""; Flags: runhidden
; Krok 1b: Ustawienie opisu usługi
Filename: "{sys}\sc.exe"; Parameters: "description SystemMonitorService ""Zbiera metryki wydajności systemu i wykonuje zdalne polecenia administracyjne."""; Flags: runhidden
; Krok 1c: Konfiguracja autostartu po awarii (Restart po 1 minucie)
Filename: "{sys}\sc.exe"; Parameters: "failure SystemMonitorService reset= 86400 actions= restart/60000/restart/60000/restart/60000"; Flags: runhidden

; Krok 2: Otwieramy plik konfiguracyjny w Notatniku
Filename: "notepad.exe"; Parameters: "{app}\appsettings.json"; Description: "Edytuj konfigurację (Wpisz adres IP serwera)"; Flags: shellexec waituntilterminated postinstall

; Krok 3: Startujemy usługę
Filename: "{sys}\sc.exe"; Parameters: "start SystemMonitorService"; Description: "Uruchom usługę monitorowania"; Flags: runhidden postinstall

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop SystemMonitorService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete SystemMonitorService"; Flags: runhidden
