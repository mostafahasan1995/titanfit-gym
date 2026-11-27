; TitanFit Server setup (admin PC): Gym.Api as a Windows service, its own PostgreSQL 17, the firewall rule,
; the first admin, and optionally the Admin app. Built in CI by installer/build-installers.ps1:
;   ISCC /DAppVersion=0.1.0 /DFileVersion=0.1.0.0 /DPayloadDir=<exes + pgsql> /DBrandDir=<images> /DRepoDir=<repo> server.iss
; Silent: TitanFit-Server-Setup-x.y.z.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART [/COMPONENTS="server"] [/LOG=file]
; Exit code 20 = files installed, but the server could not be set up or did not answer /health (see the log).

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#ifndef FileVersion
  #define FileVersion "0.0.0.0"
#endif
#ifndef PayloadDir
  #error Define PayloadDir: the folder with Gym.Api.exe, Gym.Admin.exe and pgsql
#endif
#ifndef BrandDir
  #error Define BrandDir: the folder with the wizard images and titanfit.ico
#endif
#ifndef RepoDir
  #define RepoDir ".."
#endif

[Setup]
AppId={{F9725C28-3C15-4C8C-99BA-B5D5F7DE8F2C}
AppName=TitanFit Server
AppVersion={#AppVersion}
AppVerName=TitanFit Server {#AppVersion}
AppPublisher=TITANFIT
AppCopyright=Copyright (C) TITANFIT
VersionInfoVersion={#FileVersion}
VersionInfoProductName=TitanFit Gym
VersionInfoProductTextVersion={#AppVersion}
VersionInfoDescription=TitanFit Server Setup
DefaultDirName={autopf}\TitanFit
DisableDirPage=yes
DefaultGroupName=TitanFit
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputBaseFilename=TitanFit-Server-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern dark
WizardImageFile={#BrandDir}\wizard-202x386.png,{#BrandDir}\wizard-269x515.png,{#BrandDir}\wizard-336x643.png,{#BrandDir}\wizard-430x824.png
WizardSmallImageFile={#BrandDir}\small-58.png,{#BrandDir}\small-77.png,{#BrandDir}\small-97.png,{#BrandDir}\small-124.png,{#BrandDir}\small-159.png
SetupIconFile={#BrandDir}\titanfit.ico
UninstallDisplayIcon={app}\Setup\titanfit.ico
UninstallDisplayName=TitanFit Server
SetupLogging=yes
ShowLanguageDialog=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ar"; MessagesFile: "compiler:Languages\Arabic.isl"

[Messages]
en.ConfirmUninstall=Remove %1?%n%nThe TitanFit services (server and database) and the firewall rule are removed.%n%nThe gym data is KEPT: the database, member photos and settings in C:\ProgramData\TitanFit stay on this PC, and the next Server setup uses them again.
ar.ConfirmUninstall=هل تريد إزالة %1؟%n%nستُزال خدمات TitanFit (الخادم وقاعدة البيانات) وقاعدة جدار الحماية.%n%nبيانات النادي تبقى محفوظة: قاعدة البيانات وصور الأعضاء والإعدادات في C:\ProgramData\TitanFit تبقى على هذا الجهاز، ويستخدمها إعداد الخادم القادم مرة أخرى.
en.UninstalledAll=%1 was removed. The gym data was kept in C:\ProgramData\TitanFit.
ar.UninstalledAll=تمت إزالة %1. بيانات النادي بقيت محفوظة في C:\ProgramData\TitanFit.

[CustomMessages]
en.TypeFull=Server and Admin app
ar.TypeFull=الخادم وتطبيق الإدارة
en.TypeCustom=Custom
ar.TypeCustom=مخصص
en.CompServer=TitanFit Server (API service and PostgreSQL 17 database)
ar.CompServer=خادم TitanFit (خدمة API وقاعدة بيانات PostgreSQL 17)
en.CompAdmin=Admin app on this PC (Start menu and desktop shortcuts)
ar.CompAdmin=تطبيق الإدارة على هذا الجهاز (اختصارات في قائمة ابدأ وسطح المكتب)
en.StatusConfiguring=Setting up the database and the TitanFit services. This can take a minute...
ar.StatusConfiguring=جارٍ إعداد قاعدة البيانات وخدمات TitanFit. قد يستغرق ذلك دقيقة...
en.StopFailed=The running TitanFit services could not be stopped, so the files cannot be replaced. Details: %1
ar.StopFailed=تعذر إيقاف خدمات TitanFit العاملة، لذلك لا يمكن استبدال الملفات. التفاصيل: %1
en.ConfigureFailed=The TitanFit server could not be set up or did not start (error %1).%n%nDetails: %2%n%nRun this setup again after fixing the cause. The data is kept.
ar.ConfigureFailed=تعذر إعداد خادم TitanFit أو تشغيله (الخطأ %1).%n%nالتفاصيل: %2%n%nشغّل هذا الإعداد مرة أخرى بعد إصلاح السبب. البيانات تبقى محفوظة.
en.AdminTitle=First administrator
ar.AdminTitle=المدير الأول
en.AdminDescription=The login of the gym owner or manager
ar.AdminDescription=حساب الدخول لمالك النادي أو مديره
en.AdminIntro=The server is running. Create the first administrator; this account manages staff, prices and settings. Leave every field empty to skip; run this setup again to come back.
ar.AdminIntro=الخادم يعمل. أنشئ المدير الأول؛ هذا الحساب يدير الموظفين والأسعار والإعدادات. اترك كل الحقول فارغة للتخطي، وشغّل هذا الإعداد مرة أخرى للعودة.
en.AdminFullName=Full name:
ar.AdminFullName=الاسم الكامل:
en.AdminUsername=Username (3 to 32 characters: a-z 0-9 . _ -):
ar.AdminUsername=اسم المستخدم (من 3 إلى 32 حرفًا: a-z 0-9 . _ -):
en.AdminPassword=Password (at least 6 characters):
ar.AdminPassword=كلمة المرور (6 أحرف على الأقل):
en.AdminPasswordRepeat=Repeat the password:
ar.AdminPasswordRepeat=أعد كتابة كلمة المرور:
en.AdminNeedName=Enter the full name.
ar.AdminNeedName=أدخل الاسم الكامل.
en.AdminBadUsername=The username must be 3 to 32 characters: a-z, 0-9, dot, underscore or dash.
ar.AdminBadUsername=يجب أن يتكون اسم المستخدم من 3 إلى 32 حرفًا: a-z أو 0-9 أو نقطة أو شرطة سفلية أو شرطة.
en.AdminBadPassword=The password must be 6 to 128 characters long.
ar.AdminBadPassword=يجب أن يكون طول كلمة المرور من 6 إلى 128 حرفًا.
en.AdminPasswordMismatch=The two passwords are not the same.
ar.AdminPasswordMismatch=كلمتا المرور غير متطابقتين.
en.AdminSkipConfirm=Skip creating the first administrator?%n%nNobody can log in until it exists. Run this setup again to create it.
ar.AdminSkipConfirm=هل تريد تخطي إنشاء المدير الأول؟%n%nلن يتمكن أحد من الدخول حتى يتم إنشاؤه. شغّل هذا الإعداد مرة أخرى لإنشائه.
en.AdminCreated=The administrator "%1" was created. Use it to log in to the Admin app.
ar.AdminCreated=تم إنشاء المدير "%1". استخدمه للدخول إلى تطبيق الإدارة.
en.AdminExists=An administrator already exists. Log in with it.
ar.AdminExists=يوجد مدير بالفعل. سجّل الدخول به.
en.AdminFailed=The server refused the request (HTTP %1).%n%n%2
ar.AdminFailed=رفض الخادم الطلب (HTTP %1).%n%n%2
en.AdminNoConnection=The server at %1 does not answer.
ar.AdminNoConnection=الخادم على %1 لا يستجيب.
en.ReadyTitle=Server ready
ar.ReadyTitle=الخادم جاهز
en.ReadyDescription=The address for the station PCs
ar.ReadyDescription=عنوان الخادم لأجهزة المحطات
en.ReadyText=TitanFit Server is running on this PC.%n%nIn the Station setup on each reception and cafeteria PC, enter this server address:%n%n        %1%n%nThe stations reach the server only on a Private (or Domain) network. In Windows Settings > Network, set the gym network of this PC to Private.%n%nThe database, photos and settings are in %2.
ar.ReadyText=خادم TitanFit يعمل على هذا الجهاز.%n%nفي إعداد المحطة على كل جهاز استقبال وكافتيريا، أدخل عنوان الخادم هذا:%n%n        %1%n%nتصل المحطات إلى الخادم فقط على شبكة خاصة (Private) أو شبكة مجال. من إعدادات Windows > الشبكة، اجعل شبكة النادي على هذا الجهاز خاصة.%n%nقاعدة البيانات والصور والإعدادات موجودة في %2.
en.NotReadyText=TitanFit Server is installed, but it could not be set up or did not start.%n%nDetails: %1%n%nFix the cause and run this setup again. The data is kept.
ar.NotReadyText=تم تثبيت خادم TitanFit، لكن تعذر إعداده أو تشغيله.%n%nالتفاصيل: %1%n%nأصلح السبب وشغّل هذا الإعداد مرة أخرى. البيانات تبقى محفوظة.
en.LaunchAdmin=Open the Admin app
ar.LaunchAdmin=فتح تطبيق الإدارة

[Types]
Name: "full"; Description: "{cm:TypeFull}"
Name: "custom"; Description: "{cm:TypeCustom}"; Flags: iscustom

[Components]
Name: "server"; Description: "{cm:CompServer}"; Types: full custom; Flags: fixed
Name: "admin"; Description: "{cm:CompAdmin}"; Types: full

[Dirs]
; Gym data (database, photos, keys, settings, logs): never removed by the uninstaller.
Name: "{commonappdata}\TitanFit"; Flags: uninsneveruninstall

[Files]
Source: "{#PayloadDir}\Gym.Api.exe"; DestDir: "{app}\Server"; Components: server; Flags: ignoreversion
Source: "{#RepoDir}\src\Gym.Api\appsettings.json"; DestDir: "{app}\Server"; Components: server; Flags: ignoreversion
Source: "{#PayloadDir}\pgsql\*"; DestDir: "{app}\pgsql"; Components: server; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PayloadDir}\Gym.Admin.exe"; DestDir: "{app}\Admin"; Components: admin; Flags: ignoreversion sharedfile uninsnosharedfileprompt
Source: "scripts\server-setup.ps1"; DestDir: "{app}\Setup"; Flags: ignoreversion
Source: "scripts\station-config.ps1"; DestDir: "{app}\Setup"; Flags: ignoreversion sharedfile uninsnosharedfileprompt
Source: "scripts\restore-backup.ps1"; DestDir: "{app}\Setup"; Components: server; Flags: ignoreversion
Source: "{#BrandDir}\titanfit.ico"; DestDir: "{app}\Setup"; Flags: ignoreversion sharedfile uninsnosharedfileprompt

[Icons]
Name: "{group}\TitanFit Admin"; Filename: "{app}\Admin\Gym.Admin.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: admin
Name: "{autodesktop}\TitanFit Admin"; Filename: "{app}\Admin\Gym.Admin.exe"; IconFilename: "{app}\Setup\titanfit.ico"; Components: admin

[Run]
Filename: "{app}\Admin\Gym.Admin.exe"; Description: "{cm:LaunchAdmin}"; Components: admin; Flags: postinstall nowait skipifsilent runasoriginaluser

[Code]
#include "common.iss"

const
  ApiBase = 'http://localhost:5080';

var
  AdminPage: TInputQueryWizardPage;
  ReadyPage: TOutputMsgWizardPage;
  ServicesStopped: Boolean;
  ConfigureRan: Boolean;
  ServerReady: Boolean;
  AdminNeeded: Boolean;

function SetupLogPath: String;
begin
  Result := ExpandConstant('{commonappdata}\TitanFit\logs\server-setup.log');
end;

function IsValidUsername(const Value: String): Boolean;
var
  I: Integer;
  C: Char;
begin
  Result := (Length(Value) >= 3) and (Length(Value) <= 32);
  if not Result then
    exit;
  for I := 1 to Length(Value) do
  begin
    C := Value[I];
    if not (((C >= 'a') and (C <= 'z')) or ((C >= '0') and (C <= '9')) or (C = '.') or (C = '_') or (C = '-')) then
    begin
      Result := False;
      exit;
    end;
  end;
end;

procedure InitializeWizard;
begin
  AdminPage := CreateInputQueryPage(wpInfoAfter, CustomMessage('AdminTitle'), CustomMessage('AdminDescription'),
    CustomMessage('AdminIntro'));
  AdminPage.Add(CustomMessage('AdminFullName'), False);
  AdminPage.Add(CustomMessage('AdminUsername'), False);
  AdminPage.Add(CustomMessage('AdminPassword'), True);
  AdminPage.Add(CustomMessage('AdminPasswordRepeat'), True);
  ReadyPage := CreateOutputMsgPage(AdminPage.ID, CustomMessage('ReadyTitle'), CustomMessage('ReadyDescription'), '');
end;

{ Upgrade: stop the running services so their files can be replaced. }
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  ExtractTemporaryFile('server-setup.ps1');
  ServicesStopped := True;
  if RunPowerShellScript(ExpandConstant('{tmp}\server-setup.ps1'), '-Action stop') <> 0 then
    Result := FmtMessage(CustomMessage('StopFailed'), [SetupLogPath]);
end;

{ After the files: database, config, services, firewall; then wait for /health (all in server-setup.ps1). }
procedure CurStepChanged(CurStep: TSetupStep);
var
  Params, Response: String;
  Code, Status: Integer;
begin
  if CurStep <> ssPostInstall then
    exit;
  WizardForm.StatusLabel.Caption := CustomMessage('StatusConfiguring');
  WizardForm.FilenameLabel.Caption := '';
  Params := '-Action install -AppDir "' + ExpandConstant('{app}') + '"';
  if WizardIsComponentSelected('admin') then
    Params := Params + ' -AdminApp';
  Code := RunPowerShellScript(ExpandConstant('{app}\Setup\server-setup.ps1'), Params);
  ConfigureRan := True;
  ServerReady := (Code = 0);
  if not ServerReady then
  begin
    SuppressibleMsgBox(FmtMessage(CustomMessage('ConfigureFailed'), [IntToStr(Code), SetupLogPath]), mbError, MB_OK, IDOK);
    exit;
  end;
  if HttpRequest('GET', ApiBase + '/api/setup/status', '', Status, Response) then
    AdminNeeded := (Status = 200) and (Pos('"needsSetup":true', Response) > 0);
  if AdminNeeded then
    Log('No administrator yet: the first-admin page is shown.')
  else
    Log('An administrator exists (or the status is unknown): the first-admin page is skipped.');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = AdminPage.ID then
    Result := not (ServerReady and AdminNeeded);
end;

procedure CurPageChanged(CurPageID: Integer);
var
  Address: String;
begin
  if CurPageID = ReadyPage.ID then
  begin
    Address := 'http://' + GetComputerNameString + ':5080';
    if ServerReady then
      ReadyPage.MsgLabel.Caption := FmtMessage(CustomMessage('ReadyText'), [Address, ExpandConstant('{commonappdata}\TitanFit')])
    else
      ReadyPage.MsgLabel.Caption := FmtMessage(CustomMessage('NotReadyText'), [SetupLogPath]);
  end;
end;

function CreateFirstAdmin: Boolean;
var
  FullName, UserName, Password, Body, Response, Detail: String;
  Status: Integer;
begin
  Result := False;
  FullName := Trim(AdminPage.Values[0]);
  UserName := Lowercase(Trim(AdminPage.Values[1]));
  Password := AdminPage.Values[2];

  if (FullName = '') and (UserName = '') and (Password = '') and (AdminPage.Values[3] = '') then
  begin
    Result := MsgBox(CustomMessage('AdminSkipConfirm'), mbConfirmation, MB_YESNO) = IDYES;
    exit;
  end;
  if FullName = '' then
  begin
    MsgBox(CustomMessage('AdminNeedName'), mbError, MB_OK);
    exit;
  end;
  if not IsValidUsername(UserName) then
  begin
    MsgBox(CustomMessage('AdminBadUsername'), mbError, MB_OK);
    exit;
  end;
  if (Length(Password) < 6) or (Length(Password) > 128) then
  begin
    MsgBox(CustomMessage('AdminBadPassword'), mbError, MB_OK);
    exit;
  end;
  if Password <> AdminPage.Values[3] then
  begin
    MsgBox(CustomMessage('AdminPasswordMismatch'), mbError, MB_OK);
    exit;
  end;

  Body := '{"fullName":' + JsonString(FullName) + ',"username":' + JsonString(UserName) +
    ',"password":' + JsonString(Password) + '}';
  if not HttpRequest('POST', ApiBase + '/api/setup/admin', Body, Status, Response) then
  begin
    MsgBox(FmtMessage(CustomMessage('AdminNoConnection'), [ApiBase]), mbError, MB_OK);
    exit;
  end;
  if (Status = 200) or (Status = 201) then
  begin
    MsgBox(FmtMessage(CustomMessage('AdminCreated'), [UserName]), mbInformation, MB_OK);
    Result := True;
  end
  else if Status = 409 then
  begin
    MsgBox(CustomMessage('AdminExists'), mbInformation, MB_OK);
    Result := True;
  end
  else
  begin
    Detail := JsonTextField(Response, 'detail');
    if Detail = '' then
      Detail := JsonTextField(Response, 'title');
    MsgBox(FmtMessage(CustomMessage('AdminFailed'), [IntToStr(Status), Detail]), mbError, MB_OK);
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = AdminPage.ID) and not WizardSilent then
    Result := CreateFirstAdmin;
end;

{ Silent installs report a failed server start with exit code 20. }
function GetCustomSetupExitCode: Integer;
begin
  Result := 0;
  if ConfigureRan and not ServerReady then
    Result := 20;
end;

{ Cancelled or failed after the services were stopped: start them again (the database starts with the server). }
procedure DeinitializeSetup;
var
  ResultCode: Integer;
begin
  if ServicesStopped and not ConfigureRan then
    Exec(ExpandConstant('{sys}\sc.exe'), 'start TitanFitServer', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

{ Uninstall: remove the services and the firewall rule. %ProgramData%\TitanFit (all gym data) is kept. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RunPowerShellScript(ExpandConstant('{app}\Setup\server-setup.ps1'),
      '-Action uninstall -AppDir "' + ExpandConstant('{app}') + '"');
end;
