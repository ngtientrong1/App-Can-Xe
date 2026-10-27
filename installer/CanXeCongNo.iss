; Công Nợ Cà Tiến Trọng — Inno Setup installer for the debt web app (runs as a Windows service).
; The service listens on 127.0.0.1:5200 only; remote access goes through "tailscale serve".
; Data lives in %ProgramData%\CanXeDebt and is never removed by install, upgrade or uninstall.

#define MyAppName "Công Nợ Cà Tiến Trọng"
#define MyAppNameAscii "Can Xe Cong No"
#define MyAppVersion "1.2.2"
#define MyAppPublisher "Tien Trong"
#define MyServiceName "CanXeDebt"
#define MyAppExeName "CanXe.Debt.Web.exe"
#define MyAppId "{{5E2B7C94-3D1A-4F68-8A07-C9D4E1B63F25}"
#define MyPort "5200"

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
OutputBaseFilename=CanXeCongNo-Setup-{#MyAppVersion}
SetupIconFile=..\src\CanXe.Desktop\Assets\canxe-tien-trong.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayName={#MyAppName} {#MyAppVersion}
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
CloseApplications=no
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "..\publish\debt-win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "debt\HUONG-DAN.txt"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; The service runs as LocalService, so it needs write access to its data folder.
Name: "{commonappdata}\CanXeDebt"; Permissions: users-readexec

[InternetShortcuts]
Name: "{group}\Công nợ cà (mở trong trình duyệt)"; Filename: "http://127.0.0.1:{#MyPort}/"

[Icons]
Name: "{group}\Hướng dẫn cài đặt"; Filename: "{app}\HUONG-DAN.txt"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{sys}\icacls.exe"; Parameters: """{commonappdata}\CanXeDebt"" /grant ""*S-1-5-19:(OI)(CI)M"" /T"; Flags: runhidden; StatusMsg: "Cấp quyền ghi dữ liệu cho dịch vụ..."
Filename: "{app}\HUONG-DAN.txt"; Description: "Xem hướng dẫn cài đặt"; Flags: postinstall shellexec skipifsilent

[UninstallRun]
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopDebtService"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteDebtService"

[Code]
function RunSc(const Params: String): Integer;
var
  ResultCode: Integer;
begin
  if not Exec(ExpandConstant('{sys}\sc.exe'), Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := -1;
  Result := ResultCode;
end;

procedure StopServiceAndWait();
var
  I: Integer;
begin
  { 1060 = service does not exist (first install): nothing to stop. }
  if RunSc('query {#MyServiceName}') = 1060 then
    Exit;
  RunSc('stop {#MyServiceName}');
  for I := 1 to 20 do
  begin
    Sleep(500);
    if RunSc('query {#MyServiceName}') <> 0 then
      Break;
  end;
  { Give Windows a moment to release the executable. }
  Sleep(1000);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopServiceAndWait();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Bin: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  Bin := '"' + ExpandConstant('{app}\{#MyAppExeName}') + '"';
  if RunSc('query {#MyServiceName}') = 1060 then
    RunSc('create {#MyServiceName} binPath= ' + Bin + ' start= auto obj= "NT AUTHORITY\LocalService" DisplayName= "Cong no ca Tien Trong"')
  else
    RunSc('config {#MyServiceName} binPath= ' + Bin + ' start= auto obj= "NT AUTHORITY\LocalService"');

  RunSc('description {#MyServiceName} "Web app cong no chu hang (mua ca) (127.0.0.1:{#MyPort})"');
  RunSc('failure {#MyServiceName} reset= 86400 actions= restart/5000/restart/5000/restart/5000');
  RunSc('start {#MyServiceName}');
end;
