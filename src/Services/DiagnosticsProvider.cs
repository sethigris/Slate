using System;
using System.Collections.Generic;
using System.Globalization;

namespace Slate.Services
{
    public sealed class DiagnosticsProvider
    {
        private readonly SystemInfoProvider _system;
        private readonly ProcessProvider _processes;
        private readonly DriveProvider _drives;
        private readonly WindowsServiceProvider _services;

        public DiagnosticsProvider(
            SystemInfoProvider system,
            ProcessProvider processes,
            DriveProvider drives,
            WindowsServiceProvider services)
        {
            _system = system;
            _processes = processes;
            _drives = drives;
            _services = services;
        }

        public IDictionary<string, object> GetSnapshot(bool includeServiceDetails)
        {
            Dictionary<string, object> snapshot = new Dictionary<string, object>();
            Dictionary<string, object> summary = new Dictionary<string, object>();
            snapshot["generatedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            snapshot["system"] = _system.GetSystemInfo();
            summary["processCount"] = _processes.GetTotalProcessCount();
            summary["readyDriveCount"] = _drives.GetReadyDriveCount();
            summary["driveCount"] = ((IList<IDictionary<string, object>>)_drives.ListDrives()["drives"]).Count;
            summary["serviceCounts"] = _services.GetServiceCounts();
            snapshot["summary"] = summary;

            if (includeServiceDetails)
            {
                snapshot["services"] = _services.ListServices(100, String.Empty);
            }

            return snapshot;
        }
    }
}
