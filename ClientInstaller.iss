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
; Krok 1: Instalacja usługi (ale jeszcze jej NIE startujemy)
Filename: "{sys}\sc.exe"; Parameters: "create SystemMonitorService binPath= ""{app}\SystemMonitorService.exe"" start= auto obj= LocalSystem"; Flags: runhidden

; Krok 2: Otwieramy plik konfiguracyjny w Notatniku, żeby użytkownik wpisał IP
Filename: "notepad.exe"; Parameters: "{app}\appsettings.json"; Description: "Edytuj konfigurację (Wpisz adres IP serwera)"; Flags: shellexec waituntilterminated postinstall

; Krok 3: Startujemy usługę dopiero po zamknięciu Notatnika (gdy config jest gotowy)
Filename: "{sys}\sc.exe"; Parameters: "start SystemMonitorService"; Description: "Uruchom usługę monitorowania"; Flags: runhidden postinstall

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop SystemMonitorService"; Flags: runhidden
Filename: "{sys}\sc.exe"; Parameters: "delete SystemMonitorService"; Flags: runhidden
