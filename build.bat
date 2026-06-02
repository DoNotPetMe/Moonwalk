@echo off
REM ===========================================================================
REM  Build MoonwalkPro.exe locally (only needed if you ever install AutoHotkey).
REM  Most people don't need this - just download the prebuilt .exe from the
REM  GitHub "Actions" tab artifact. See README section "Get the compiled app".
REM
REM  Requires AutoHotkey v2 installed. Adjust the path below if yours differs.
REM ===========================================================================

set "AHK_DIR=%ProgramFiles%\AutoHotkey"
set "AHK2EXE=%AHK_DIR%\Compiler\Ahk2Exe.exe"
set "BASE=%AHK_DIR%\v2\AutoHotkey64.exe"

if not exist "%AHK2EXE%" (
  echo Could not find Ahk2Exe at "%AHK2EXE%".
  echo Install AutoHotkey v2 from https://www.autohotkey.com/ or edit this file.
  pause
  exit /b 1
)

"%AHK2EXE%" /in "moonwalk.ahk" /out "MoonwalkPro.exe" /base "%BASE%"

if exist "MoonwalkPro.exe" (
  echo Done -> MoonwalkPro.exe
) else (
  echo Build failed.
)
pause
