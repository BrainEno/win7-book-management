Unicode True
Name "BOOK DESK 简易图书管理系统"
OutFile "output\Win7BookManagement-Offline-Setup.exe"
InstallDir "$PROGRAMFILES\Win7BookManagement"
InstallDirRegKey HKLM "Software\BOOK DESK\Win7BookManagement" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
ShowInstDetails show
ShowUninstDetails show
BrandingText "BOOK DESK · 完全离线图书进销存"

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "x64.nsh"

!define APP_VERSION "0.4.0"
!define DOTNET48_FILE "NDP48-x86-x64-AllOS-ENU.exe"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "欢迎安装 BOOK DESK"
!define MUI_WELCOMEPAGE_TEXT "这是一个完全离线运行的小型书店进销存软件。$\r$\n$\r$\n安装程序会自动携带应用所需 DLL；如果这台电脑缺少 .NET Framework 4.8，也会直接使用安装包内部的离线运行时安装，不需要联网下载。$\r$\n$\r$\n安装完成后，第一次打开程序会自动出现逐步新手引导。"
!define MUI_FINISHPAGE_TITLE "BOOK DESK 已安装完成"
!define MUI_FINISHPAGE_TEXT "桌面已经创建“BOOK DESK 图书管理系统”快捷方式。$\r$\n$\r$\n第一次打开后请跟随新手引导操作。若本次安装了 .NET Framework 4.8 并提示重启，请先重启电脑再打开程序。"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "SimpChinese"
!insertmacro MUI_LANGUAGE "English"

Var DotNetMissing
Var DotNetResult

Function CheckDotNet48
  StrCpy $DotNetMissing "1"

  SetRegView 32
  ClearErrors
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${IfNot} ${Errors}
    ${If} $0 >= 528040
      StrCpy $DotNetMissing "0"
      Return
    ${EndIf}
  ${EndIf}

  ${If} ${RunningX64}
    SetRegView 64
    ClearErrors
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
    ${IfNot} ${Errors}
      ${If} $0 >= 528040
        StrCpy $DotNetMissing "0"
      ${EndIf}
    ${EndIf}
    SetRegView 32
  ${EndIf}
FunctionEnd

Function .onInit
  ${IfNot} ${AtLeastWin7}
    MessageBox MB_OK|MB_ICONSTOP "本软件至少需要 Windows 7 SP1。当前系统版本过低，无法安装。"
    Abort
  ${EndIf}

  ${If} ${IsWin7}
  ${AndIfNot} ${AtLeastServicePack} 1
    MessageBox MB_OK|MB_ICONSTOP "检测到 Windows 7，但没有 Service Pack 1。请先安装 Windows 7 SP1 后再安装 BOOK DESK。"
    Abort
  ${EndIf}

  Call CheckDotNet48
FunctionEnd

Section "-Prerequisites"
  ${If} $DotNetMissing == "1"
    SetOutPath "$TEMP\Win7BookManagementPrereq"
    File /oname=${DOTNET48_FILE} "prerequisites\${DOTNET48_FILE}"

    DetailPrint "正在离线安装 Microsoft .NET Framework 4.8..."
    ExecWait '"$TEMP\Win7BookManagementPrereq\${DOTNET48_FILE}" /q /norestart' $DotNetResult

    ${If} $DotNetResult == 0
      DetailPrint ".NET Framework 4.8 安装完成。"
    ${ElseIf} $DotNetResult == 3010
      SetRebootFlag true
      DetailPrint ".NET Framework 4.8 安装完成，需要重新启动 Windows。"
    ${ElseIf} $DotNetResult == 1641
      SetRebootFlag true
      DetailPrint ".NET Framework 4.8 安装完成，需要重新启动 Windows。"
    ${Else}
      MessageBox MB_OK|MB_ICONSTOP ".NET Framework 4.8 离线安装失败（代码 $DotNetResult）。BOOK DESK 尚未安装。"
      Abort
    ${EndIf}

    Delete "$TEMP\Win7BookManagementPrereq\${DOTNET48_FILE}"
    RMDir "$TEMP\Win7BookManagementPrereq"
  ${EndIf}
SectionEnd

Section "BOOK DESK" SecMain
  SectionIn RO
  SetOutPath "$INSTDIR"
  File /r "..\src\Win7BookManagement\bin\x86\Release\*.*"

  WriteRegStr HKLM "Software\BOOK DESK\Win7BookManagement" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\BOOK DESK"
  CreateShortCut "$SMPROGRAMS\BOOK DESK\BOOK DESK 图书管理系统.lnk" "$INSTDIR\Win7BookManagement.exe"
  CreateShortCut "$DESKTOP\BOOK DESK 图书管理系统.lnk" "$INSTDIR\Win7BookManagement.exe"

  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement" "DisplayName" "BOOK DESK 简易图书管理系统"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement" "Publisher" "BOOK DESK"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement" "DisplayIcon" "$INSTDIR\Win7BookManagement.exe"
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement" "NoRepair" 1

  ${If} $DotNetMissing == "1"
    MessageBox MB_OK|MB_ICONINFORMATION "BOOK DESK 已安装。$\r$\n$\r$\n这台电脑刚刚补装了 .NET Framework 4.8。为保证 Windows 7 稳定运行，建议现在重新启动电脑，然后双击桌面的 BOOK DESK 图标。"
  ${Else}
    MessageBox MB_OK|MB_ICONINFORMATION "BOOK DESK 已安装完成。$\r$\n$\r$\n请双击桌面的“BOOK DESK 图书管理系统”图标。第一次打开后会自动出现详细新手引导。"
  ${EndIf}
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\BOOK DESK 图书管理系统.lnk"
  Delete "$SMPROGRAMS\BOOK DESK\BOOK DESK 图书管理系统.lnk"
  RMDir "$SMPROGRAMS\BOOK DESK"

  RMDir /r "$INSTDIR"

  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\Win7BookManagement"
  DeleteRegKey HKLM "Software\BOOK DESK\Win7BookManagement"

  MessageBox MB_OK|MB_ICONINFORMATION "程序文件已卸载。您的书店数据库和备份不会被删除。"
SectionEnd
