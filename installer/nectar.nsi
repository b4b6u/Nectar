; Nectar installer (NSIS 3).
;
; Why this exists: the MSIX needs a trusted code-signing certificate before Windows will install
; it, and a self-signed one is not trusted anywhere else. This installer is a plain .exe that
; copies the self-contained build into %LOCALAPPDATA%\Programs\Nectar and makes shortcuts - no
; administrator rights, no certificate import, no Windows App Installer.
;
; Build with scripts/package-installer.ps1, which passes APP_VERSION, APP_SOURCE, OUT_DIR and
; ICON_FILE. Requires nothing but the self-contained publish folder.

Unicode true
!include "MUI2.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "1.0.0"
!endif
!ifndef APP_SOURCE
  !define APP_SOURCE "..\build\installer\payload"
!endif
!ifndef OUT_DIR
  !define OUT_DIR "..\dist"
!endif
!ifndef ICON_FILE
  !define ICON_FILE "..\Nectar\Assets\nectar.ico"
!endif

Name "Nectar"
OutFile "${OUT_DIR}\Nectar-Setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\Nectar"
InstallDirRegKey HKCU "Software\Nectar" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 64
BrandingText "Nectar ${APP_VERSION}"
Icon "${ICON_FILE}"
UninstallIcon "${ICON_FILE}"
XPStyle on

VIProductVersion "${APP_VERSION}.0"
VIAddVersionKey "ProductName" "Nectar"
VIAddVersionKey "FileDescription" "Nectar setup"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"
VIAddVersionKey "LegalCopyright" "MIT"

!define MUI_ICON "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\Nectar.exe"
!define MUI_FINISHPAGE_RUN_TEXT "Start Nectar now (it opens hidden - press Alt + N)"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section "Nectar" SecMain
  ; A running copy holds its DLLs open, so close it before replacing files.
  ExecWait 'taskkill /IM Nectar.exe /F'

  SetOutPath "$INSTDIR"
  File /r "${APP_SOURCE}\*.*"

  CreateShortcut "$DESKTOP\Nectar.lnk" "$INSTDIR\Nectar.exe" "" "$INSTDIR\Assets\nectar.ico" 0
  CreateDirectory "$SMPROGRAMS\Nectar"
  CreateShortcut "$SMPROGRAMS\Nectar\Nectar.lnk" "$INSTDIR\Nectar.exe" "" "$INSTDIR\Assets\nectar.ico" 0
  CreateShortcut "$SMPROGRAMS\Nectar\Uninstall Nectar.lnk" "$INSTDIR\Uninstall.exe" "" "$INSTDIR\Uninstall.exe" 0

  WriteUninstaller "$INSTDIR\Uninstall.exe"

  WriteRegStr HKCU "Software\Nectar" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "DisplayName" "Nectar"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "Publisher" "Nectar"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "DisplayIcon" "$INSTDIR\Assets\nectar.ico"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ExecWait 'taskkill /IM Nectar.exe /F'

  Delete "$DESKTOP\Nectar.lnk"
  Delete "$SMPROGRAMS\Nectar\Nectar.lnk"
  Delete "$SMPROGRAMS\Nectar\Uninstall Nectar.lnk"
  RMDir "$SMPROGRAMS\Nectar"

  ; The app's data lives in %LOCALAPPDATA%\Nectar (tasks, note, settings), not here, so a reinstall
  ; keeps it. Delete that folder by hand to start clean.
  RMDir /r "$INSTDIR"

  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\Nectar"
  DeleteRegKey HKCU "Software\Nectar"
SectionEnd
