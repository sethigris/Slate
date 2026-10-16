@echo off
setlocal
netsh http add urlacl url=http://127.0.0.1:8765/ user=%USERDOMAIN%\%USERNAME%
if errorlevel 1 (
  echo If this failed, run this file from an elevated Command Prompt.
  exit /b 1
)
echo URL reservation added for the current user on port 8765.
echo If you change the port in slate.config.json, update this URL reservation too.
