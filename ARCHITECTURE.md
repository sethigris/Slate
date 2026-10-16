# Slate architecture

Slate is a single-process Windows desktop app that also hosts a loopback HTTP API. Its UI is a manually composed Windows Forms interface compatible with C# 5 and .NET Framework 4.5-era APIs. The API server remains separate from the UI form and continues to use the existing request-routing, authentication, rate-limiting, serialization, collector, and logging layers.

## Runtime flow

1. `Program.Main` initializes WinForms and opens `Slate.UI.MainForm`.
2. `MainForm` loads or creates `slate.config.json` beside the executable.
3. The form creates a `FileLogger`, constructs `ApiServer`, and starts its listener automatically.
4. A background health checker requests `/api/v1/health` using the current Bearer key. Its result updates the status badge without blocking the UI thread.
5. Sidebar views call the same read-only system collectors used by the API and display their results in a consistent desktop interface.
6. Closing the form stops and disposes the HTTP server and logger. The app does not install a Windows service or persist a background process after closing.

## User interface views

- Overview: API online/offline state, base address, process/drive/service counts, and operating system uptime. On an HTTP.sys permission error, a guided action can request UAC approval for the built-in `netsh` URL reservation setup and retry startup.
- System: flattened system information key/value grid.
- Processes: filterable process table with ID, name, working-set/private memory, threads, and start time.
- Storage: drive capacity and availability table.
- Services: filterable Windows service table. The UI does not perform service actions.
- Diagnostics: formatted JSON snapshot.
- API access: local URL, key reveal/copy/rotation controls, and endpoint reference.
- Activity log: recent entries from the rolling log file.

## Core layers

- `Configuration/AppConfig.cs`: configuration validation and random API-key creation.
- `Core/ApiServer.cs`: HTTP.sys listener, request worker limits, request/response handling, shutdown.
- `Core/RouteDispatcher.cs`: GET-only API routing.
- `Security/ApiKeyAuthenticator.cs`: Bearer token extraction and constant-time comparison.
- `Security/RateLimiter.cs`: per-client request limiting.
- `Services/*Provider.cs`: system, process, drive, service and diagnostics read operations.
- `Serialization/JsonUtil.cs`: `JavaScriptSerializer` wrapper.
- `Infrastructure/FileLogger.cs`: rolling application/request log.
- `UI/MainForm.cs`: WinForms shell, navigation, collector views, API key controls and asynchronous health polling.
- `Testing/SelfTests.cs`: console-visible unit-style tests.

## Build targets

`build.bat` compiles the `WinExe` GUI target as `bin\Slate.exe`. `test.bat` recompiles the same source set with `SLATE_CONSOLE` defined as `bin\Slate.Tests.exe`, then invokes its built-in self-tests. Both scripts prefer the 64-bit .NET Framework C# compiler and fall back to the 32-bit Framework compiler.

The app intentionally uses standard .NET Framework references only. No ASP.NET Core, NuGet packages, browser control, or third-party GUI toolkit is required.

## Security boundaries

- Listener is restricted to the `127.0.0.1` prefix.
- All API routes, including health, require the configured Bearer API key.
- The API is GET-only and read-only.
- Request bodies, long URLs, excess concurrency, and excess per-client request rates are constrained.
- Process command lines, environment variables, user profile paths, and file contents are not returned.
- API key remains a local plaintext config value; filesystem and Windows account security protect it.

## Lifecycle note

Slate only hosts the API while its desktop process is open. Closing the window stops the listener. It does not run as a Windows service, auto-start at login, or keep running silently after the UI closes.
