using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using Slate.Configuration;
using Slate.Services;

namespace Slate.Core
{
    public sealed class RouteDispatcher
    {
        private readonly AppConfig _config;
        private readonly SystemInfoProvider _system;
        private readonly ProcessProvider _processes;
        private readonly DriveProvider _drives;
        private readonly WindowsServiceProvider _services;
        private readonly DiagnosticsProvider _diagnostics;

        public RouteDispatcher(AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            _config = config;
            _system = new SystemInfoProvider();
            _processes = new ProcessProvider();
            _drives = new DriveProvider();
            _services = new WindowsServiceProvider();
            _diagnostics = new DiagnosticsProvider(_system, _processes, _drives, _services);
        }

        public ApiResponse Dispatch(HttpListenerRequest request)
        {
            if (request == null || request.Url == null)
            {
                return ApiResponse.Error(400, "invalid_request", "The request URL is not valid.");
            }

            if (!String.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse.Error(405, "method_not_allowed", "Only GET requests are supported.")
                    .AddHeader("Allow", "GET");
            }

            string path = NormalizePath(request.Url.AbsolutePath);
            if (String.Equals(path, "/api/v1/health", StringComparison.OrdinalIgnoreCase))
            {
                return HandleHealth();
            }

            if (String.Equals(path, "/api/v1/system", StringComparison.OrdinalIgnoreCase))
            {
                return HandleSystem();
            }

            if (String.Equals(path, "/api/v1/processes", StringComparison.OrdinalIgnoreCase))
            {
                return HandleProcesses(request);
            }

            if (path.StartsWith("/api/v1/processes/", StringComparison.OrdinalIgnoreCase))
            {
                return HandleSingleProcess(path.Substring("/api/v1/processes/".Length));
            }

            if (String.Equals(path, "/api/v1/drives", StringComparison.OrdinalIgnoreCase))
            {
                return HandleDrives();
            }

            if (String.Equals(path, "/api/v1/services", StringComparison.OrdinalIgnoreCase))
            {
                return HandleServices(request);
            }

            if (path.StartsWith("/api/v1/services/", StringComparison.OrdinalIgnoreCase))
            {
                return HandleSingleService(path.Substring("/api/v1/services/".Length));
            }

            if (String.Equals(path, "/api/v1/diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                return HandleDiagnostics();
            }

            if (String.Equals(path, "/api/v1/version", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object> body = new Dictionary<string, object>();
                body["success"] = true;
                body["name"] = "Slate";
                body["version"] = Program.Version;
                body["apiVersion"] = "v1";
                return ApiResponse.Ok(body);
            }

            return ApiResponse.Error(404, "not_found", "No API route matches this path.");
        }

        private ApiResponse HandleHealth()
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["status"] = "healthy";
            body["name"] = "Slate";
            body["version"] = Program.Version;
            body["timeUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            return ApiResponse.Ok(body);
        }

        private ApiResponse HandleSystem()
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["system"] = _system.GetSystemInfo();
            return ApiResponse.Ok(body);
        }

        private ApiResponse HandleProcesses(HttpListenerRequest request)
        {
            int limit;
            string error;
            if (!RequestUtilities.TryGetLimit(
                request.QueryString,
                "limit",
                Math.Min(50, _config.MaxProcessResults),
                _config.MaxProcessResults,
                out limit,
                out error))
            {
                return ApiResponse.Error(400, "invalid_parameter", error);
            }

            string filter = RequestUtilities.GetSafeFilter(request.QueryString["name"], 80);
            IDictionary<string, object> data = _processes.ListProcesses(limit, filter);
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["data"] = data;
            return ApiResponse.Ok(body);
        }

        private ApiResponse HandleSingleProcess(string rawId)
        {
            int processId;
            if (!RequestUtilities.TryParseProcessId(rawId, out processId))
            {
                return ApiResponse.Error(400, "invalid_process_id", "The process ID must be a positive integer.");
            }

            IDictionary<string, object> process = _processes.GetProcess(processId);
            if (process == null)
            {
                return ApiResponse.Error(404, "process_not_found", "The requested process could not be found or inspected.");
            }

            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["process"] = process;
            return ApiResponse.Ok(body);
        }

        private ApiResponse HandleDrives()
        {
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["data"] = _drives.ListDrives();
            return ApiResponse.Ok(body);
        }

        private ApiResponse HandleServices(HttpListenerRequest request)
        {
            int limit;
            string error;
            if (!RequestUtilities.TryGetLimit(
                request.QueryString,
                "limit",
                Math.Min(100, _config.MaxServiceResults),
                _config.MaxServiceResults,
                out limit,
                out error))
            {
                return ApiResponse.Error(400, "invalid_parameter", error);
            }

            string filter = RequestUtilities.GetSafeFilter(request.QueryString["name"], 100);
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["data"] = _services.ListServices(limit, filter);
            return ApiResponse.Ok(body);
        }

        private ApiResponse HandleSingleService(string rawName)
        {
            string serviceName;
            try
            {
                serviceName = Uri.UnescapeDataString(rawName);
            }
            catch (UriFormatException)
            {
                return ApiResponse.Error(400, "invalid_service_name", "The service name is not valid URL encoding.");
            }

            if (String.IsNullOrWhiteSpace(serviceName) || serviceName.Length > 256 ||
                serviceName.IndexOf('/') >= 0 || serviceName.IndexOf('\\') >= 0)
            {
                return ApiResponse.Error(400, "invalid_service_name", "The service name is not valid.");
            }

            IDictionary<string, object> service = _services.GetService(serviceName);
            if (service == null)
            {
                return ApiResponse.Error(404, "service_not_found", "The requested service could not be found or inspected.");
            }

            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["service"] = service;
            return ApiResponse.Ok(body);
        }

        private ApiResponse HandleDiagnostics()
        {
            IDictionary<string, object> snapshot = _diagnostics.GetSnapshot(
                _config.IncludeServiceDetailsInDiagnostics);
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = true;
            body["diagnostics"] = snapshot;
            return ApiResponse.Ok(body);
        }

        private static string NormalizePath(string path)
        {
            if (String.IsNullOrEmpty(path))
            {
                return "/";
            }

            string result = path;
            while (result.Length > 1 && result.EndsWith("/", StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - 1);
            }

            return result;
        }
    }
}
