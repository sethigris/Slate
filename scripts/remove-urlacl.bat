@echo off
setlocal
netsh http delete urlacl url=http://127.0.0.1:8765/
if errorlevel 1 exit /b 1
echo URL reservation removed for port 8765.
