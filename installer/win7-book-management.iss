[Setup]
AppId={{9A6D9FD1-16BC-46B6-A65A-0D8C8A53805A}
AppName=Win7 Book Management
AppVersion=0.1.0
DefaultDirName={autopf}\Win7BookManagement
DefaultGroupName=Win7 Book Management
OutputDir=output
OutputBaseFilename=Win7BookManagement-Setup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
MinVersion=6.1sp1
UninstallDisplayIcon={app}\Win7BookManagement.exe

[Dirs]
Name: "{commonappdata}\Win7BookManagement"; Permissions: users-modify
Name: "{commonappdata}\Win7BookManagement\data"; Permissions: users-modify
Name: "{commonappdata}\Win7BookManagement\backup"; Permissions: users-modify
Name: "{commonappdata}\Win7BookManagement\exports"; Permissions: users-modify

[Files]
Source: "..\src\Win7BookManagement\bin\x86\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\图书管理系统"; Filename: "{app}\Win7BookManagement.exe"
Name: "{commondesktop}\图书管理系统"; Filename: "{app}\Win7BookManagement.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "其他选项："

[Run]
Filename: "{app}\Win7BookManagement.exe"; Description: "启动图书管理系统"; Flags: nowait postinstall skipifsilent
