@echo off
setlocal
cd /d "%~dp0"
if not exist bin mkdir bin
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Could not find the .NET Framework C# compiler.
  echo Checked Framework64 and Framework v4.0.30319 locations.
  pause
  exit /b 1
)
if exist bin\Slate.Tests.exe del /q bin\Slate.Tests.exe
echo Building console test runner...
"%CSC%" /nologo /target:exe /define:SLATE_CONSOLE /optimize+ /debug- /langversion:5 /codepage:65001 /out:bin\Slate.Tests.exe /r:System.dll /r:System.Core.dll /r:System.Web.Extensions.dll /r:System.ServiceProcess.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll src\Program.cs src\UI\MainForm.cs src\Configuration\AppConfig.cs src\Core\ApiResponse.cs src\Core\ApiServer.cs src\Core\RequestUtilities.cs src\Core\RouteDispatcher.cs src\Infrastructure\FileLogger.cs src\Security\ApiKeyAuthenticator.cs src\Security\RateLimiter.cs src\Serialization\JsonUtil.cs src\Services\DiagnosticsProvider.cs src\Services\DriveProvider.cs src\Services\ProcessProvider.cs src\Services\SystemInfoProvider.cs src\Services\WindowsServiceProvider.cs src\Testing\SelfTests.cs
if errorlevel 1 (
  echo Test runner compilation failed.
  pause
  exit /b 1
)
echo.
bin\Slate.Tests.exe --self-test
set "TEST_RESULT=%ERRORLEVEL%"
echo.
if "%TEST_RESULT%"=="0" (
  echo ALL SELF-TESTS PASSED.
) else (
  echo SOME SELF-TESTS FAILED. See output above.
)
pause
exit /b %TEST_RESULT%
