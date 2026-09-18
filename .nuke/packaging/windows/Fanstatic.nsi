Unicode true
Name "Fanstatic"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\Fanstatic"
RequestExecutionLevel user

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
SectionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\Fanstatic\Fanstatic.lnk"
  Delete "$SMPROGRAMS\Fanstatic\Uninstall.lnk"
  RMDir "$SMPROGRAMS\Fanstatic"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Fanstatic"
  RMDir /r "$INSTDIR"
SectionEnd
