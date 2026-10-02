#define AppName "BOOK DESK 简易图书管理系统"
#define AppVersion "0.4.0"
#define AppPublisher "BOOK DESK"
#define AppExeName "Win7BookManagement.exe"
#define DotNetInstaller "NDP48-x86-x64-AllOS-ENU.exe"

[Setup]
AppId={{9A6D9FD1-16BC-46B6-A65A-0D8C8A53805A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Win7BookManagement
DefaultGroupName=BOOK DESK
OutputDir=output
OutputBaseFilename=Win7BookManagement-Offline-Setup
Compression=lzma2/max
SolidCompression=yes
PrivilegesRequired=admin
MinVersion=6.1sp1
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
DisableProgramGroupPage=yes
SetupLogging=yes
RestartIfNeededByRun=yes
InfoBeforeFile=install-info.txt
SetupIconFile=..\src\Win7BookManagement\Resources\BookDesk.ico

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "在桌面创建快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Dirs]
Name: "{commonappdata}\Win7BookManagement"; Permissions: users-modify
Name: "{commonappdata}\Win7BookManagement\data"; Permissions: users-modify
Name: "{commonappdata}\Win7BookManagement\backup"; Permissions: users-modify
Name: "{commonappdata}\Win7BookManagement\exports"; Permissions: users-modify

[Files]
Source: "..\src\Win7BookManagement\bin\x86\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "prerequisites\{#DotNetInstaller}"; Flags: dontcopy

[Icons]
Name: "{group}\BOOK DESK 图书管理系统"; Filename: "{app}\{#AppExeName}"
Name: "{commondesktop}\BOOK DESK 图书管理系统"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "安装完成后启动 BOOK DESK"; Flags: nowait postinstall skipifsilent; Check: CanLaunchImmediately

[Code]
var
  DotNetWasMissing: Boolean;

function IsDotNet48Installed: Boolean;
var
  ReleaseValue: Cardinal;
begin
  Result :=
    RegQueryDWordValue(
      HKLM32,
      'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
      'Release',
      ReleaseValue) and (ReleaseValue >= 528040);

  if (not Result) and IsWin64 then
    Result :=
      RegQueryDWordValue(
        HKLM64,
        'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full',
        'Release',
        ReleaseValue) and (ReleaseValue >= 528040);
end;

function InitializeSetup: Boolean;
begin
  DotNetWasMissing := not IsDotNet48Installed;
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  InstallerPath: String;
begin
  Result := '';

  if not DotNetWasMissing then
    exit;

  ExtractTemporaryFile('{#DotNetInstaller}');
  InstallerPath := ExpandConstant('{tmp}\{#DotNetInstaller}');

  WizardForm.StatusLabel.Caption :=
    '正在离线安装 Microsoft .NET Framework 4.8，请稍候……';

  if not Exec(
    InstallerPath,
    '/q /norestart',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    Result := '无法启动 Microsoft .NET Framework 4.8 离线安装程序。';
    exit;
  end;

  if ResultCode = 0 then
  begin
    Result := '';
  end
  else if (ResultCode = 3010) or (ResultCode = 1641) then
  begin
    NeedsRestart := True;
    Result := '';
  end
  else
  begin
    Result :=
      'Microsoft .NET Framework 4.8 安装失败（错误代码 ' +
      IntToStr(ResultCode) + '）。安装已停止。';
  end;
end;

function CanLaunchImmediately: Boolean;
begin
  Result := not DotNetWasMissing;
end;