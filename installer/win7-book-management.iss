#define AppName "BOOK DESK 简易图书管理系统"
#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
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
Name: "{commonappdata}\Win7BookManagement\logs"; Permissions: users-modify

[Files]
Source: "..\src\Win7BookManagement\bin\x86\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "prerequisites\{#DotNetInstaller}"; Flags: dontcopy

[Icons]
Name: "{group}\BOOK DESK 图书管理系统"; Filename: "{app}\{#AppExeName}"
Name: "{commondesktop}\BOOK DESK 图书管理系统"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{dotnet40}\ngen.exe"; Parameters: "install ""{app}\{#AppExeName}"" /nologo /silent"; StatusMsg: "正在优化首次启动速度，请稍候……"; Flags: runhidden; Check: CanOptimize
Filename: "{app}\{#AppExeName}"; Description: "安装完成后启动 BOOK DESK"; Flags: nowait postinstall skipifsilent; Check: CanLaunchImmediately

[UninstallRun]
Filename: "{dotnet40}\ngen.exe"; Parameters: "uninstall ""{app}\{#AppExeName}"" /nologo /silent"; Flags: runhidden; RunOnceId: "BookDeskNgen"

[Code]
var
  DotNetWasMissing: Boolean;
  DotNetInstallSucceeded: Boolean;
  DotNetRestartRequired: Boolean;
  DotNetLogDirectory: String;

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

function IsHotfixInstalled(const Kb: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
  RootKey: Integer;
  State: Cardinal;
  Key: String;
begin
  Result := False;
  RootKey := HKLM32;
  if IsWin64 then RootKey := HKLM64;
  Key := 'SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\Packages';
  if RegGetSubkeyNames(RootKey, Key, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if (Pos(Kb + '~', Names[I]) > 0) and
         RegQueryDWordValue(RootKey, Key + '\' + Names[I], 'CurrentState', State) and
         (State = 112) then
      begin
        Result := True;
        exit;
      end;
end;

function InitializeSetup: Boolean;
begin
  DotNetWasMissing := not IsDotNet48Installed;
  DotNetInstallSucceeded := not DotNetWasMissing;
  DotNetRestartRequired := False;
  Result := True;
  if DotNetWasMissing and (GetWindowsVersion < $06020000) and
     (not (IsHotfixInstalled('KB4474419') and IsHotfixInstalled('KB4490628'))) then
    Result := MsgBox(
      '这台 Windows 7 缺少 .NET 4.8 所需的 SHA-2 / 服务栈补丁（KB4474419、KB4490628）。' + #13#10 +
      '请先从 Microsoft Update Catalog 获取对应 x86/x64 补丁，安装并重启后再运行本程序。' + #13#10#13#10 +
      '仍要继续尝试吗？建议选择“否”退出。', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  InstallerPath: String;
  Hint: String;
begin
  Result := '';

  if not DotNetWasMissing then
    exit;

  ExtractTemporaryFile('{#DotNetInstaller}');
  InstallerPath := ExpandConstant('{tmp}\{#DotNetInstaller}');

  DotNetLogDirectory := ExpandConstant('{commonappdata}\Win7BookManagement\logs\dotnet48');
  ForceDirectories(DotNetLogDirectory);
  WizardForm.StatusLabel.Caption := '正在安装 .NET Framework 4.8，预计 10–20 分钟，请勿关机。';
  WizardForm.Update;

  if not Exec(
    InstallerPath,
    '/passive /norestart /log "' + DotNetLogDirectory + '"',
    '',
    SW_SHOW,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    Result := '无法启动 .NET Framework 4.8 离线安装程序。日志目录：' + DotNetLogDirectory;
    exit;
  end;

  if ResultCode = 0 then
  begin
    DotNetInstallSucceeded := True;
    Result := '';
  end
  else if (ResultCode = 3010) or (ResultCode = 1641) then
  begin
    DotNetInstallSucceeded := True;
    DotNetRestartRequired := True;
    NeedsRestart := True;
    Result := '';
  end
  else
  begin
    case ResultCode of
      1602: Hint := '安装被取消。';
      1603: Hint := '安装失败，常见原因是缺少 SHA-2 补丁、待重启或系统组件损坏。';
      5100: Hint := '系统不满足前置条件，请检查 Win7 SP1 和 KB4474419 / KB4490628。';
    else Hint := '请重启电脑后重试，并把日志交给维护人员。';
    end;
    Result := '.NET Framework 4.8 安装失败（错误代码 ' + IntToStr(ResultCode) + '）。' +
      Hint + #13#10 + '日志目录：' + DotNetLogDirectory;
  end;
end;

function CanLaunchImmediately: Boolean;
begin
  Result := DotNetInstallSucceeded and (not DotNetRestartRequired);
end;

function CanOptimize: Boolean;
begin
  Result := IsDotNet48Installed and FileExists(ExpandConstant('{dotnet40}\ngen.exe'));
end;
