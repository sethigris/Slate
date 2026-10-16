@echo off
setlocal
cd /d "%~dp0"
if not exist bin mkdir bin
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find the .NET Framework C# compiler.
  echo Checked Framework64 and Framework v4.0.30319 locations.
  echo Install a compatible .NET Framework Developer Pack or Visual Studio Build Tools.
  pause
  exit /b 1
)
if exist bin\Slate.exe del /q bin\Slate.exe
if exist bin\Slate.build-version del /q bin\Slate.build-version
echo Building Slate desktop application...
echo Compiler: %CSC%
"%CSC%" /nologo /target:winexe /optimize+ /debug- /langversion:5 /codepage:65001 /out:bin\Slate.exe /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll /r:System.ServiceProcess.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Program.cs src\UI\MainForm.cs src\Configuration\AppConfig.cs src\Core\ApiResponse.cs src\Core\ApiServer.cs src\Core\RequestUtilities.cs src\Core\RouteDispatcher.cs src\Infrastructure\FileLogger.cs src\Security\ApiKeyAuthenticator.cs src\Security\RateLimiter.cs src\Serialization\JsonUtil.cs src\Services\DiagnosticsProvider.cs src\Services\DriveProvider.cs src\Services\ProcessProvider.cs src\Services\SystemInfoProvider.cs src\Services\WindowsServiceProvider.cs src\Testing\SelfTests.cs
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Read the compiler errors above.
  pause
  exit /b 1
)
echo.
>bin\Slate.build-version echo Slate GUI 1.1.0
echo BUILD SUCCEEDED: bin\Slate.exe
echo Double-click Slate.exe or run run.bat to open the desktop app.
exit /b 0
