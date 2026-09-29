#define AppName "BOOK DESK 简易图书管理系统"
#define AppVersion "0.4.0"
#define DotNetInstaller "NDP48-x86-x64-AllOS-ENU.exe"

[Setup]
AppId={{9A6D9FD1-16BC-46B6-A65A-0D8C8A53805A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=BOOK DESK
DefaultDirName={autopf}Win7BookManagement
DefaultGroupName=BOOK DESK
OutputDir=output
OutputBaseFilename=Win7BookManagement-Offline-Setup
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=admin
MinVersion=6.1sp1
UninstallDisplayIcon={app}Win7BookManagement.exe
WizardStyle=modern
DisableProgramGroupPage=yes
SetupLogging=yes
RestartIfNeededByRun=yes
InfoBeforeFile=install-info.txt

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:LanguagesChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Dirs]
Name: "{commonappdata}Win7BookManagement"; Permissions: users-modify
Name: "{commonappdata}Win7BookManagementdata"; Permissions: users-modify
Name: "{commonappdata}Win7BookManagementackup"; Permissions: users-modify
Name: "{commonappdata}Win7BookManagementexports"; Permissions: users-modify

[Files]
Source: "..srcWin7BookManagementinRelease*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "prerequisites{#DotNetInstaller}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: NeedsDotNet48

[Icons]
Name: "{group}BOOK DESK 图书管理系统"; Filename: "{app}Win7BookManagement.exe"
Name: "{commondesktop}BOOK DESK 图书管理系统"; Filename: "{app}Win7BookManagement.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "在桌面创建快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Run]
Filename: "{tmp}{#DotNetInstaller}"; Parameters: "/q /norestart"; StatusMsg: "正在离线安装 Microsoft .NET Framework 4.8，请稍候..."; Check: NeedsDotNet48; Flags: waituntilterminated; ReturnCodes: 0 1641 3010
Filename: "{app}Win7BookManagement.exe"; Description: "安装完成后启动 BOOK DESK"; Flags: nowait postinstall skipifsilent; Check: CanLaunchImmediately

[Code]
var
  DotNetWasMissing: Boolean;

function IsDotNet48Installed: Boolean;
var
  ReleaseValue: Cardinal;
begin
  Result :=
    (RegQueryDWordValue(
      HKLM32,
      'SOFTWAREMicrosoftNET Framework SetupNDP4Full',
      'Release',
      ReleaseValue) and (ReleaseValue >= 528040));

  if (not Result) and IsWin64 then
    Result :=
      (RegQueryDWordValue(
        HKLM64,
        'SOFTWAREMicrosoftNET Framework SetupNDP4Full',
        'Release',
        ReleaseValue) and (ReleaseValue >= 528040));
end;

function InitializeSetup: Boolean;
begin
  DotNetWasMissing := not IsDotNet48Installed;
  Result := True;
end;

function NeedsDotNet48: Boolean;
begin
  Result := DotNetWasMissing;
end;

function CanLaunchImmediately: Boolean;
begin
  { If .NET 4.8 had to be installed, avoid launching the app immediately.
    Some Windows 7 machines need one reboot after the framework installation. }
  Result := not DotNetWasMissing;
end;
