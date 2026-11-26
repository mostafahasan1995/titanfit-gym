; TitanFit Station setup (reception and cafeteria PCs): the Reception and/or Cafeteria app (and the Admin app if wanted),
; shortcuts, and %ProgramData%\TitanFit\station.json with the server address. Built in CI by installer/build-installers.ps1.
; Silent: TitanFit-Station-Setup-x.y.z.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /COMPONENTS="reception,cafeteria"
;         /SERVERURL=http://SERVER-PC:5080 [/LOG=file]
; Exit code 1 = invalid /SERVERURL, 7 = no app chosen, 21 = station.json could not be written.

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#ifndef FileVersion
  #define FileVersion "0.0.0.0"
#endif
#ifndef PayloadDir
  #error Define PayloadDir: the folder with Gym.Reception.exe, Gym.Cafeteria.exe and Gym.Admin.exe
#endif
#ifndef BrandDir
  #error Define BrandDir: the folder with the wizard images and titanfit.ico
#endif

[Setup]
AppId={{5891D355-E549-4044-8A8D-82BD75ADCEEA}
AppName=TitanFit Station
AppVersion={#AppVersion}
AppVerName=TitanFit Station {#AppVersion}
AppPublisher=TITANFIT
AppCopyright=Copyright (C) TITANFIT
VersionInfoVersion={#FileVersion}
VersionInfoProductName=TitanFit Gym
VersionInfoProductTextVersion={#AppVersion}
VersionInfoDescription=TitanFit Station Setup
DefaultDirName={autopf}\TitanFit
DisableDirPage=yes
DefaultGroupName=TitanFit
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputBaseFilename=TitanFit-Station-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern dark
WizardImageFile={#BrandDir}\wizard-202x386.png,{#BrandDir}\wizard-269x515.png,{#BrandDir}\wizard-336x643.png,{#BrandDir}\wizard-430x824.png
WizardSmallImageFile={#BrandDir}\small-58.png,{#BrandDir}\small-77.png,{#BrandDir}\small-97.png,{#BrandDir}\small-124.png,{#BrandDir}\small-159.png
SetupIconFile={#BrandDir}\titanfit.ico
UninstallDisplayIcon={app}\Setup\titanfit.ico
UninstallDisplayName=TitanFit Station
SetupLogging=yes
ShowLanguageDialog=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"

[Messages]
en.ConfirmUninstall=Remove %1?%n%nThe apps and their shortcuts are removed. The station settings (C:\ProgramData\TitanFit\station.json) are kept for the next install.
ar.ConfirmUninstall=هل تريد إزالة %1؟%n%nستُزال التطبيقات واختصاراتها. إعدادات المحطة (C:\ProgramData\TitanFit\station.json) تبقى محفوظة للتثبيت القادم.
en.UninstalledAll=%1 was removed. The station settings were kept in C:\ProgramData\TitanFit\station.json.
ar.UninstalledAll=تمت إزالة %1. إعدادات المحطة بقيت في C:\ProgramData\TitanFit\station.json.

[CustomMessages]
en.TypeCustom=Choose the apps for this PC
ar.TypeCustom=اختر التطبيقات لهذا الجهاز
en.CompReception=Reception (members, photos, check-in)
ar.CompReception=الاستقبال (الأعضاء والصور وتسجيل الدخول)
en.CompCafeteria=Cafeteria (cashier and stock)
ar.CompCafeteria=الكافتيريا (الكاشير والمخزون)
en.CompAdmin=Admin app
ar.CompAdmin=تطبيق الإدارة
en.NoComponent=Choose at least one app: Reception, Cafeteria or Admin.
ar.NoComponent=اختر تطبيقًا واحدًا على الأقل: الاستقبال أو الكافتيريا أو الإدارة.
en.ServerTitle=Server address
ar.ServerTitle=عنوان الخادم
en.ServerDescription=Where is the TitanFit Server?
ar.ServerDescription=أين يوجد خادم TitanFit؟
en.ServerIntro=Enter the address shown at the end of the Server setup on the admin PC: http:// + the admin PC's name or IP address + :5080.
ar.ServerIntro=أدخل العنوان الظاهر في نهاية إعداد الخادم على جهاز الإدارة: http:// ثم اسم جهاز الإدارة أو عنوان IP الخاص به ثم :5080.
en.ServerLabel=Server address (for example http://SERVER-PC:5080):
ar.ServerLabel=عنوان الخادم (مثل http://SERVER-PC:5080):
en.InvalidServerUrl=The server address "%1" is not valid.%n%nUse the form http://SERVER-PC:5080: http or https, the PC name or IP address, an optional port, no path.
ar.InvalidServerUrl=عنوان الخادم "%1" غير صالح.%n%nاستخدم الشكل http://SERVER-PC:5080: http أو https، ثم اسم الجهاز أو عنوان IP، ثم منفذ اختياري، بدون مسار.
en.ServerUnreachable=The server %1 does not answer right now.%n%nInstall anyway? The apps connect as soon as the server runs and this PC can reach it.
ar.ServerUnreachable=الخادم %1 لا يستجيب الآن.%n%nهل تريد التثبيت على أي حال؟ تتصل التطبيقات عندما يعمل الخادم ويستطيع هذا الجهاز الوصول إليه.
en.ConfigFailed=The station settings %1 could not be written (error %2).
ar.ConfigFailed=تعذرت كتابة إعدادات المحطة %1 (الخطأ %2).
en.LaunchReception=Open Reception
ar.LaunchReception=فتح الاستقبال
en.LaunchCafeteria=Open Cafeteria
ar.LaunchCafeteria=فتح الكافتيريا
en.LaunchAdmin=Open the Admin app
ar.LaunchAdmin=فتح تطبيق الإدارة

[Types]
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "reception"; Description: "{cm:CompReception}"
Name: "cafeteria"; Description: "{cm:CompCafeteria}"
Name: "admin"; Description: "{cm:CompAdmin}"

[Dirs]
; station.json lives here; never removed by the uninstaller.
Name: "{commonappdata}\TitanFit"; Flags: uninsneveruninstall

[Files]
Source: "{#PayloadDir}\Gym.Reception.exe"; DestDir: "{app}\Reception"; Components: reception; Flags: ignoreversion
Source: "{#PayloadDir}\Gym.Cafeteria.exe"; DestDir: "{app}\Cafeteria"; Components: cafeteria; Flags: ignoreversion
Source: "{#PayloadDir}\Gym.Admin.exe"; DestDir: "{app}\Admin"; Components: admin; Flags: ignoreversion sharedfile uninsnosharedfileprompt
Source: "scripts\station-config.ps1"; DestDir: "{app}\Setup"; Flags: ignoreversion sharedfile uninsnosharedfileprompt
Source: "{#BrandDir}\titanfit.ico"; DestDir: "{app}\Setup"; Flags: ignoreversion sharedfile uninsnosharedfileprompt

[Icons]
Name: "{group}\TitanFit Reception"; Filename: "{app}\Reception\Gym.Reception.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: reception
Name: "{autodesktop}\TitanFit Reception"; Filename: "{app}\Reception\Gym.Reception.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: reception
Name: "{group}\TitanFit Cafeteria"; Filename: "{app}\Cafeteria\Gym.Cafeteria.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: cafeteria
Name: "{autodesktop}\TitanFit Cafeteria"; Filename: "{app}\Cafeteria\Gym.Cafeteria.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: cafeteria
Name: "{group}\TitanFit Admin"; Filename: "{app}\Admin\Gym.Admin.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: admin
Name: "{autodesktop}\TitanFit Admin"; Filename: "{app}\Admin\Gym.Admin.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: admin

[Run]
Filename: "{app}\Reception\Gym.Reception.exe"; Description: "{cm:LaunchReception}"; Components: reception; Flags: postinstall nowait skipifsilent runasoriginaluser
Filename: "{app}\Cafeteria\Gym.Cafeteria.exe"; Description: "{cm:LaunchCafeteria}"; Components: cafeteria; Flags: postinstall nowait skipifsilent runasoriginaluser unchecked
Filename: "{app}\Admin\Gym.Admin.exe"; Description: "{cm:LaunchAdmin}"; Components: admin; Flags: postinstall nowait skipifsilent runasoriginaluser unchecked

[Code]
#include "common.iss"

const
  DefaultServerUrl = 'http://SERVER-PC:5080';

var
  ServerPage: TInputQueryWizardPage;
  ConfigureRan: Boolean;
  ConfigOk: Boolean;

function StationConfigPath: String;
begin
  Result := ExpandConstant('{commonappdata}\TitanFit\station.json');
end;

{ Trimmed, without trailing slashes. }
function NormalizeServerUrl(const Value: String): String;
begin
  Result := Trim(Value);
  while Length(Result) > 0 do
  begin
    if Result[Length(Result)] <> '/' then
      break;
    Result := Copy(Result, 1, Length(Result) - 1);
  end;
end;

function IsHostChar(C: Char): Boolean;
begin
  Result := ((C >= 'a') and (C <= 'z')) or ((C >= 'A') and (C <= 'Z')) or ((C >= '0') and (C <= '9')) or
    (C = '-') or (C = '.');
end;

{ http(s)://host[:port] with a PC name, a DNS name or an IPv4 address; no path, query or user info. }
function IsValidServerUrl(const Value: String): Boolean;
var
  Url, Rest, Host, Port: String;
  P, I, PortNumber: Integer;
begin
  Result := False;
  Url := NormalizeServerUrl(Value);
  if Pos('http://', Lowercase(Url)) = 1 then
    Rest := Copy(Url, 8, Length(Url))
  else if Pos('https://', Lowercase(Url)) = 1 then
    Rest := Copy(Url, 9, Length(Url))
  else
    exit;

  P := Pos(':', Rest);
  if P > 0 then
  begin
    Host := Copy(Rest, 1, P - 1);
    Port := Copy(Rest, P + 1, Length(Rest));
    if (Length(Port) < 1) or (Length(Port) > 5) then
      exit;
    for I := 1 to Length(Port) do
      if (Port[I] < '0') or (Port[I] > '9') then
        exit;
    PortNumber := StrToIntDef(Port, 0);
    if (PortNumber < 1) or (PortNumber > 65535) then
      exit;
  end
  else
    Host := Rest;

  if Length(Host) = 0 then
    exit;
  if (Host[1] = '.') or (Host[1] = '-') then
    exit;
  if (Host[Length(Host)] = '.') or (Host[Length(Host)] = '-') then
    exit;
  for I := 1 to Length(Host) do
    if not IsHostChar(Host[I]) then
      exit;
  Result := True;
end;

{ The "serverUrl" of an existing station.json, or ''. }
function ExistingServerUrl: String;
var
  Raw: AnsiString;
  Text: String;
  P: Integer;
begin
  Result := '';
  if not LoadStringFromFile(StationConfigPath, Raw) then
    exit;
  Text := String(Raw);
  P := Pos('"serverurl"', Lowercase(Text));
  if P = 0 then
    exit;
  Text := Copy(Text, P + 11, Length(Text));
  P := Pos('"', Text);
  if P = 0 then
    exit;
  Text := Copy(Text, P + 1, Length(Text));
  P := Pos('"', Text);
  if P > 0 then
    Result := Copy(Text, 1, P - 1);
end;

function InitialServerUrl: String;
begin
  Result := NormalizeServerUrl(ExpandConstant('{param:SERVERURL|}'));
  if Result = '' then
    Result := ExistingServerUrl;
  if Result = '' then
    Result := DefaultServerUrl;
end;

function InitializeSetup: Boolean;
var
  Url: String;
begin
  Result := True;
  Url := NormalizeServerUrl(ExpandConstant('{param:SERVERURL|}'));
  if (Url <> '') and not IsValidServerUrl(Url) then
  begin
    Log('Invalid /SERVERURL: ' + Url);
    SuppressibleMsgBox(FmtMessage(CustomMessage('InvalidServerUrl'), [Url]), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpSelectComponents, CustomMessage('ServerTitle'), CustomMessage('ServerDescription'),
    CustomMessage('ServerIntro'));
  ServerPage.Add(CustomMessage('ServerLabel'), False);
  ServerPage.Values[0] := InitialServerUrl;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  Url, Response: String;
  Status: Integer;
begin
  Result := True;
  if WizardSilent then
    exit;   { silent installs are checked in InitializeSetup and PrepareToInstall }

  if CurPageID = wpSelectComponents then
  begin
    if WizardSelectedComponents(False) = '' then
    begin
      MsgBox(CustomMessage('NoComponent'), mbError, MB_OK);
      Result := False;
    end;
  end
  else if CurPageID = ServerPage.ID then
  begin
    Url := NormalizeServerUrl(ServerPage.Values[0]);
    if not IsValidServerUrl(Url) then
    begin
      MsgBox(FmtMessage(CustomMessage('InvalidServerUrl'), [Url]), mbError, MB_OK);
      Result := False;
      exit;
    end;
    ServerPage.Values[0] := Url;
    if HttpRequest('GET', Url + '/health', '', Status, Response) then
      if Status = 200 then
        exit;
    Result := MsgBox(FmtMessage(CustomMessage('ServerUnreachable'), [Url]), mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if WizardSelectedComponents(False) = '' then
    Result := CustomMessage('NoComponent');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
begin
  if CurStep <> ssPostInstall then
    exit;
  Code := RunPowerShellScript(ExpandConstant('{app}\Setup\station-config.ps1'),
    '-ServerUrl "' + NormalizeServerUrl(ServerPage.Values[0]) + '"');
  ConfigureRan := True;
  ConfigOk := (Code = 0);
  if not ConfigOk then
    SuppressibleMsgBox(FmtMessage(CustomMessage('ConfigFailed'), [StationConfigPath, IntToStr(Code)]), mbError, MB_OK, IDOK);
end;

function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if ConfigureRan and not ConfigOk then
    Result := 21;
end;
