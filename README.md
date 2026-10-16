# Slate

**Slate is a minimal Windows desktop app and local system API.** Open one window to inspect your computer, check whether the API is responding, browse processes, drives and Windows services, review diagnostics and activity, and copy the API key for trusted local integrations.

- **Platform:** Windows 10
- **Language:** C# 5.0
- **Runtime:** .NET Framework 4.5-compatible APIs
- **Dependencies:** .NET Framework only; no third-party packages
- **Default listener:** `http://127.0.0.1:8765/`
- **Security model:** loopback-only, API-key authenticated, read-only endpoints

## Start here

1. Extract the ZIP into a writable folder.
2. Double-click `run.bat`. It builds `bin\Slate.exe` if needed, then opens the desktop window.
3. Slate starts the local API automatically.
4. Select **API access** in the left sidebar to reveal or copy the API key. Keep it private.

After the first successful build, you can open `bin\Slate.exe` directly.

There is no need to open PowerShell or a browser for everyday use. The desktop app contains these views:

| View | What it does |
|---|---|
| Overview | Shows API health, local URL, process count, ready drives, service count, and Windows uptime; start or stop the API. |
| System | Shows Windows, computer, processor, runtime, and uptime information. |
| Processes | Lists visible processes, memory use, thread count, and start time; filter by process name. |
| Storage | Lists detected drives, readiness, total capacity, free space, and use percentage. |
| Services | Lists Windows service names, display names, status, type, and whether they can be stopped. Slate does not change service state. |
| Diagnostics | Displays a combined read-only snapshot as formatted JSON. |
| API access | Copies the local URL or key, regenerates the key, and documents available endpoints. |
| Activity log | Shows recent request and application log entries. |

Slate checks its own health endpoint periodically. The top status indicator shows `API ONLINE`, `NO RESPONSE`, `NEEDS ATTENTION`, or `STOPPED`. If startup fails, the overview explains the likely cause; the Activity log also records request outcomes and errors.

## Build and test

The project intentionally targets C# 5 and uses the legacy .NET Framework compiler. From a Command Prompt opened in the project folder:

```bat
build.bat
test.bat
```

`build.bat` compiles the GUI application as `bin\Slate.exe`. `test.bat` builds a separate console test runner (`bin\Slate.Tests.exe`) so test output stays visible in the terminal. `run.bat` launches the graphical app.

The scripts look for `csc.exe` in both `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\` and `%WINDIR%\Microsoft.NET\Framework\v4.0.30319\`. If neither exists, install a compatible .NET Framework developer/compiler environment.

## API usage

The desktop app is the main interface for people. The HTTP API is for developers, scripts, and trusted local programs that want to read system information without parsing the UI.

Every route requires:

```text
Authorization: Bearer YOUR_API_KEY
```

Replace `YOUR_API_KEY` with the value shown in **API access**.

### PowerShell example

```powershell
$headers = @{ Authorization = 'Bearer YOUR_API_KEY' }
Invoke-RestMethod -Uri 'http://127.0.0.1:8765/api/v1/system' -Headers $headers -TimeoutSec 5
```

### curl example

```bat
curl.exe -H "Authorization: Bearer YOUR_API_KEY" http://127.0.0.1:8765/api/v1/health
```

These requests are optional for everyday users; they are useful when integrating Slate with another application.

## Endpoints

All routes use `GET` and require Bearer authentication:

| Endpoint | Purpose |
|---|---|
| `/api/v1/health` | Returns health status and version. |
| `/api/v1/version` | Returns application and API version. |
| `/api/v1/system` | Returns machine name, Windows/runtime version information, processor count, architecture, uptime, and UTC time. |
| `/api/v1/processes` | Lists processes, sorted by process ID. |
| `/api/v1/processes/{pid}` | Returns metrics for one process ID. |
| `/api/v1/drives` | Lists mounted drives and capacity data when available. |
| `/api/v1/services` | Lists Windows services and statuses. |
| `/api/v1/services/{name}` | Returns details for one service. |
| `/api/v1/diagnostics` | Returns a combined snapshot with system, process, drive, and service summary data. |

Query parameters:

- `/api/v1/processes?limit=50` sets the maximum process records; `name` filters by process name.
- `/api/v1/services?limit=100` sets the maximum service records; `name` filters service name and display name.

Invalid limits receive HTTP 400. The API returns structured JSON errors and an `X-Request-Id`. Authorization values and query strings are omitted from request logs.

## Configuration and first-run setup

Slate creates `slate.config.json` beside the executable on first start and generates a random 256-bit API key. The GUI can reveal/copy the key and regenerate it. Regenerating the key invalidates the old key, so update any clients that use it.

The configuration file includes settings such as port, API key, log file, rate limits, worker limits, and response limits. Stop Slate before manually editing the configuration; the next start validates the file. The desktop app stores a relative log path as an absolute path beside the app when it first loads the configuration.

### HTTP.sys URL reservation

`HttpListener` uses Windows HTTP.sys. Some Windows installations may deny permission to listen on the local URL. If Slate reports `Access is denied`, select **Fix permissions** on the Overview page and approve the Windows administrator prompt. Slate asks the built-in `netsh` tool to grant the configured local URL to the current Windows account, then retries the API startup.

Manual fallback: right-click Command Prompt and choose **Run as administrator**, then run `scripts\setup-urlacl.bat`. That helper is configured for port 8765; if you use a different port, update the URL reservation to match. The included `scripts\remove-urlacl.bat` removes the default reservation.

If the port is already occupied, stop the other listener or change `Port` in `slate.config.json` while Slate is closed.

## Security notes

- The listener binds to `127.0.0.1`; it is not exposed to other computers on the network.
- Every API route requires a Bearer API key.
- The API is read-only and does not run commands, stop processes, or modify Windows services.
- It does not return process command lines, environment variables, user profile paths, or file contents.
- The key is stored in plaintext in `slate.config.json` so Slate can authenticate clients. Protect your Windows account and do not share the config file.
- Local malware running as the same Windows user may be able to read the key or call the local API. Slate is not intended to be internet-facing.

## Troubleshooting

**Slate says `NEEDS ATTENTION` on startup**

Read the message on Overview. For `Access is denied`, select **Fix permissions** and approve the Windows administrator prompt. For a port conflict, stop the other listener or use another port.

**Slate shows `NO RESPONSE`**

The listener was created but the health endpoint did not return a valid response. Check Activity log and select **Stop API**, then **Start API**. A timeout means the check did not receive a timely response; it does not by itself prove the entire process has crashed.

**Some process fields are unavailable**

Windows may deny access to protected processes, and a process may exit while Slate inspects it. The API returns partial information rather than failing the whole list.

**The services view has unavailable details**

Some service status details require permissions. Slate inspects service state only and does not elevate itself.

## Version

Slate desktop interface and local API: `1.1.0`.
