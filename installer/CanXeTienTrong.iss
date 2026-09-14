; Cân Xe Tiến Trọng — Inno Setup installer script (Phase 8 / 1.0.7)
; Does NOT touch %LocalAppData%\CanXe (database, settings, logs, backups).

#define MyAppName "Cân Xe Tiến Trọng"
#define MyAppNameAscii "Can Xe Tien Trong"
#define MyAppVersion "1.0.7"
#define MyAppPublisher "Tien Trong"
#define MyAppExeName "CanXe.Desktop.exe"
#define MyAppId "{{A8F3C2E1-7B4D-4E9A-9C1F-2D6E8B5A0F31}"
#define MyResetHelperExeName "CanXe.ComResetHelper.exe"
#define MyResetTaskFolder "CanXeTienTrong"
#define MyResetTaskLeaf "ResetComPort"
#define MyResetTaskName MyResetTaskFolder + "\" + MyResetTaskLeaf

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppNameAscii}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\releases
OutputBaseFilename=CanXeTienTrong-Setup-1.0.7
SetupIconFile=..\src\CanXe.Desktop\Assets\canxe-tien-trong.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName} {#MyAppVersion}
VersionInfoVersion=1.0.7.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
CloseApplications=yes
RestartApplications=no
; Uninstall removes only installed app files under {app}. User data under
; %LocalAppData%\CanXe is intentionally never deleted by this installer.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; Entire publish output. Never write into {localappdata}\CanXe from this installer.
Source: "..\publish\win10-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Shared IPC folder: the standard-user app writes the COM-reset request/reads the result here,
; and the SYSTEM-run Scheduled Task (CanXe.ComResetHelper.exe) writes the result back. Needs to be
; writable by standard users, unlike the rest of {commonappdata}.
Name: "{commonappdata}\CanXe"; Permissions: users-modify

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; CanXe.Desktop runs as a standard user (no UAC prompt). Recovering a wedged USB-to-serial
; adapter needs elevated pnputil calls, so that one privileged step is delegated to a Scheduled
; Task that runs as SYSTEM — registered here (installer is already elevated) with a security
; descriptor that lets any standard user trigger it via "schtasks /Run" without a UAC prompt.
Filename: "{sys}\schtasks.exe"; Parameters: "/Create /TN ""{#MyResetTaskName}"" /TR ""\""{app}\{#MyResetHelperExeName}\"""" /SC ONDEMAND /RU SYSTEM /RL HIGHEST /F"; Flags: runhidden; StatusMsg: "Configuring automatic COM port recovery..."
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -Command ""$s=New-Object -ComObject Schedule.Service; $s.Connect(); $f=$s.GetFolder('\{#MyResetTaskFolder}'); $t=$f.GetTask('{#MyResetTaskLeaf}'); $t.SetSecurityDescriptor('D:(A;;GA;;;BA)(A;;GA;;;SY)(A;;GRGX;;;AU)',0)"""; Flags: runhidden
; Disable "USB selective suspend" on the active power plan so the PL2303HXD USB-to-serial adapter
; is never power-suspended by Windows (a common cause of it going silent until physically
; unplugged/replugged). Uses the same power-scheme GUIDs Power Options' own "USB settings" UI
; writes. The app also re-applies this at every startup (see UsbSelectiveSuspendConfigurator) in
; case a future power-plan reset (e.g. Windows Update) turns it back on.
Filename: "{sys}\powercfg.exe"; Parameters: "/setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0"; Flags: runhidden; StatusMsg: "Disabling USB selective suspend..."
Filename: "{sys}\powercfg.exe"; Parameters: "/setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0"; Flags: runhidden
Filename: "{sys}\powercfg.exe"; Parameters: "/setactive SCHEME_CURRENT"; Flags: runhidden
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""{#MyResetTaskName}"" /F"; Flags: runhidden; RunOnceId: "RemoveResetComPortTask"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
