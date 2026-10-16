@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"
set "NEED_BUILD=0"
if not exist bin\Slate.exe set "NEED_BUILD=1"
if not exist bin\Slate.build-version set "NEED_BUILD=1"
if exist bin\Slate.build-version (
  set "BUILD_MARKER="
  set /p BUILD_MARKER=<bin\Slate.build-version
  if not "!BUILD_MARKER!"=="Slate GUI 1.1.0" set "NEED_BUILD=1"
)
if "%NEED_BUILD%"=="1" (
  call build.bat
  if errorlevel 1 exit /b 1
)
start "" "%~dp0bin\Slate.exe"
exit /b 0
