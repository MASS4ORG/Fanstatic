Unicode true
Name "Fanstatic"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Fanstatic"
RequestExecutionLevel user

!include "WinMessages.nsh"

Page directory
Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "Fanstatic"
  SetOutPath "$INSTDIR"
  File /r "${SOURCE_DIR}\*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Fanstatic"
  CreateShortcut "$SMPROGRAMS\Fanstatic\Fanstatic.lnk" "$INSTDIR\fanstatic.exe"
  CreateShortcut "$SMPROGRAMS\Fanstatic\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "DisplayName" "Fanstatic"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "Publisher" "Bruno Massa"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  StrCpy $0 "add"
  Call UpdateUserPath
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\Fanstatic\Fanstatic.lnk"
  Delete "$SMPROGRAMS\Fanstatic\Uninstall.lnk"
  RMDir "$SMPROGRAMS\Fanstatic"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic"
  StrCpy $0 "remove"
  Call un.UpdateUserPath
  RMDir /r "$INSTDIR"
SectionEnd

; Adds ($0 = "add") or removes ($0 = "remove") $INSTDIR in the user PATH through user-path.ps1, because NSIS strings
; are capped at 1024 characters and would truncate a long PATH. Warns instead of failing when PowerShell errors.
!macro UPDATE_USER_PATH PREFIX
Function ${PREFIX}UpdateUserPath
  InitPluginsDir
  File "/oname=$PLUGINSDIR\user-path.ps1" "user-path.ps1"
  nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\user-path.ps1" $0 "$INSTDIR"'
  Pop $1
  StrCmp $1 "0" UpdateUserPathBroadcast
  MessageBox MB_OK|MB_ICONEXCLAMATION "Could not update the user PATH ($1). Please $0 $INSTDIR manually." /SD IDOK
  Return
UpdateUserPathBroadcast:
  SendMessage ${HWND_BROADCAST} ${WM_WININICHANGE} 0 "STR:Environment" /TIMEOUT=5000
FunctionEnd
!macroend

!insertmacro UPDATE_USER_PATH ""
!insertmacro UPDATE_USER_PATH "un."
