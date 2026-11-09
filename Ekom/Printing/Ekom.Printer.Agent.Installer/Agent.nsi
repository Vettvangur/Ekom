Unicode true
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!ifndef AGENT_VERSION
  !error "Build using build/Build-PrinterAgent.ps1"
!endif
Name "Ekom Printer Agent"
OutFile "${OUTPUT_FILE}"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
VIProductVersion "${AGENT_VERSION}.0"
VIAddVersionKey /LANG=1033 "ProductName" "Ekom Printer Agent"
VIAddVersionKey /LANG=1033 "FileDescription" "Ekom Printer Agent installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${AGENT_VERSION}"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Ekom"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_TEXT "Installation complete. Open Ekom Printer Agent Setup from the Start menu to configure the station and test the printer."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "Ekom Printer Agent requires 64-bit Windows." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  ; Fixed application-owned location. Do not allow /D to redirect recursive cleanup.
  StrCpy $INSTDIR "$PROGRAMFILES64\Ekom\Printer Agent"
  SetShellVarContext all
FunctionEnd

Section "Install"
  InitPluginsDir
  SetOutPath "$PLUGINSDIR\payload\Agent"
  File /r /x *.pdb "${AGENT_PAYLOAD}\*"
  SetOutPath "$PLUGINSDIR\payload\Setup"
  File /r /x *.pdb "${SETUP_PAYLOAD}\*"
  SetOutPath "$PLUGINSDIR"
  File "Manage-Agent.ps1"
  ClearErrors
  ExecWait '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\Manage-Agent.ps1" -Action Install -PayloadDirectory "$PLUGINSDIR\payload" -Version "${AGENT_VERSION}"' $0
  ${If} ${Errors}
    StrCpy $0 1
  ${EndIf}
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Installation failed. See %ProgramData%\Ekom\PrinterAgent\installer.log. Any previous installation has been kept for recovery." /SD IDOK
    SetErrorLevel $0
    Abort
  ${EndIf}
  ClearErrors
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  ${If} ${Errors}
    MessageBox MB_ICONSTOP "The service was installed, but the uninstaller could not be written. Run this installer again to repair it." /SD IDOK
    SetErrorLevel 1
    Abort
  ${EndIf}
  SetErrorLevel 0
SectionEnd

Function un.onInit
  StrCpy $INSTDIR "$PROGRAMFILES64\Ekom\Printer Agent"
  SetShellVarContext all
FunctionEnd

Section "Uninstall"
  InitPluginsDir
  SetOutPath "$PLUGINSDIR"
  File "Manage-Agent.ps1"
  ClearErrors
  ExecWait '"$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\Manage-Agent.ps1" -Action Uninstall' $0
  ${If} ${Errors}
    StrCpy $0 1
  ${EndIf}
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Uninstall could not complete. Close the setup application and check the installer log, then retry. Station recovery data has been retained." /SD IDOK
    SetErrorLevel $0
    Abort
  ${EndIf}
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  SetErrorLevel 0
SectionEnd
