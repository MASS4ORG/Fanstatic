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
  File /r "${SOURCE_DIR}/*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\Fanstatic"
  CreateShortcut "$SMPROGRAMS\Fanstatic\Fanstatic.lnk" "$INSTDIR\fanstatic.exe"
  CreateShortcut "$SMPROGRAMS\Fanstatic\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "DisplayName" "Fanstatic"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "Publisher" "Bruno Massa"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  Call AddToPath
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\Fanstatic\Fanstatic.lnk"
  Delete "$SMPROGRAMS\Fanstatic\Uninstall.lnk"
  RMDir "$SMPROGRAMS\Fanstatic"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic"
  Call un.RemoveFromPath
  RMDir /r "$INSTDIR"
SectionEnd

; Adds $INSTDIR to the user PATH (HKCU\Environment) if not already present.
; $0 = current PATH, $1 = $INSTDIR.
Function AddToPath
  ReadRegStr $0 HKCU "Environment" "Path"
  StrCpy $1 "$INSTDIR"
  Call IsInPath
  IfErrors PathNotPresent
  Goto AddToPathDone
PathNotPresent:
  StrCmp $0 "" 0 PathAppend
  WriteRegExpandStr HKCU "Environment" "Path" "$1"
  Goto BroadcastPathChange
PathAppend:
  StrCpy $2 "$0;$1"
  WriteRegExpandStr HKCU "Environment" "Path" "$2"
BroadcastPathChange:
  SendMessage ${HWND_BROADCAST} ${WM_WININICHANGE} 0 "STR:Environment" /TIMEOUT=5000
AddToPathDone:
FunctionEnd

; Substring check: error flag set if $1 is not contained in $0.
; Uses $3-$7.
Function IsInPath
  StrLen $3 "$0"
  StrLen $4 "$1"
  StrCmp $1 "" IsInPathError
  IntCmp $3 $4 0 IsInPathError IsInPathScan
IsInPathScan:
  IntOp $5 $3 - $4
  StrCpy $6 0
IsInPathLoop:
  StrCpy $7 "$0" $4 $6
  StrCmp $7 "$1" IsInPathFound
  IntCmp $6 $5 IsInPathError IsInPathNext IsInPathError
IsInPathNext:
  IntOp $6 $6 + 1
  Goto IsInPathLoop
IsInPathFound:
  ClearErrors
  Return
IsInPathError:
  SetErrors
  Return
FunctionEnd

; Removes $INSTDIR from the user PATH, keeping every other entry intact.
; $0 = $INSTDIR, $1 = original PATH, $2 = rebuilt PATH, $3 = remaining input, $4 = current entry.
Function un.RemoveFromPath
  ReadRegStr $1 HKCU "Environment" "Path"
  StrCmp $1 "" RemoveFromPathDone
  StrCpy $0 "$INSTDIR"
  StrCpy $2 ""
  StrCpy $3 "$1"
  StrCpy $4 ""
RemoveFromPathLoop:
  StrLen $5 $3
  StrCmp $5 0 RemoveFromPathFlush
  StrCpy $5 $3 1
  StrCpy $3 $3 "" 1
  StrCmp $5 ";" 0 RemoveFromPathAccum
  Call un.RemoveEntry
  StrCpy $4 ""
  Goto RemoveFromPathLoop
RemoveFromPathAccum:
  StrCpy $4 "$4$5"
  Goto RemoveFromPathLoop
RemoveFromPathFlush:
  Call un.RemoveEntry
  StrCmp $2 "$1" RemoveFromPathDone
  StrCmp $2 "" 0 RemoveFromPathWrite
  DeleteRegValue HKCU "Environment" "Path"
  Goto RemoveFromPathBroadcast
RemoveFromPathWrite:
  WriteRegExpandStr HKCU "Environment" "Path" "$2"
RemoveFromPathBroadcast:
  SendMessage ${HWND_BROADCAST} ${WM_WININICHANGE} 0 "STR:Environment" /TIMEOUT=5000
RemoveFromPathDone:
FunctionEnd

; Appends the accumulated PATH entry in $4 to $2 unless it equals $INSTDIR ($0) or is empty.
Function un.RemoveEntry
  StrCmp $4 "" RemoveEntrySkip
  StrCmp $4 $0 RemoveEntrySkip
  StrCmp $2 "" 0 RemoveEntrySeparator
  StrCpy $2 "$4"
  Goto RemoveEntryDone
RemoveEntrySeparator:
  StrCpy $2 "$2;$4"
RemoveEntryDone:
RemoveEntrySkip:
FunctionEnd