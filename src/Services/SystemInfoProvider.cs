using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Slate.Services
{
    public sealed class SystemInfoProvider
    {
        [DllImport("kernel32.dll", EntryPoint = "GetTickCount64")]
        private static extern ulong GetTickCount64Native();

        public IDictionary<string, object> GetSystemInfo()
        {
            Dictionary<string, object> info = new Dictionary<string, object>();
            IDictionary<string, object> windows = GetWindowsVersion();
            TimeSpan uptime = GetUptime();

            info["machineName"] = Environment.MachineName;
            info["os"] = windows;
            info["osVersionFallback"] = Environment.OSVersion.VersionString;
            info["processorCount"] = Environment.ProcessorCount;
            info["is64BitOperatingSystem"] = Environment.Is64BitOperatingSystem;
            info["is64BitProcess"] = Environment.Is64BitProcess;
            info["frameworkVersion"] = Environment.Version.ToString();
            info["runtimeDirectory"] = Environment.SystemDirectory;
            info["uptimeSeconds"] = (long)Math.Max(0, uptime.TotalSeconds);
            info["uptime"] = FormatDuration(uptime);
            info["currentTimeUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            return info;
        }

        public TimeSpan GetUptime()
        {
            try
            {
                ulong milliseconds = GetTickCount64Native();
                return TimeSpan.FromMilliseconds(milliseconds);
            }
            catch (EntryPointNotFoundException)
            {
                int ticks = Environment.TickCount;
                if (ticks < 0)
                {
                    ticks = 0;
                }
                return TimeSpan.FromMilliseconds(ticks);
            }
            catch (DllNotFoundException)
            {
                int ticks = Environment.TickCount;
                if (ticks < 0)
                {
                    ticks = 0;
                }
                return TimeSpan.FromMilliseconds(ticks);
            }
        }

        private static IDictionary<string, object> GetWindowsVersion()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["productName"] = Environment.OSVersion.Platform.ToString();
            result["displayVersion"] = String.Empty;
            result["build"] = String.Empty;
            result["ubr"] = null;

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false))
                {
                    if (key != null)
                    {
                        SetIfPresent(result, "productName", key.GetValue("ProductName"));
                        SetIfPresent(result, "displayVersion", key.GetValue("DisplayVersion"));
                        if (String.IsNullOrEmpty(Convert.ToString(result["displayVersion"], CultureInfo.InvariantCulture)))
                        {
                            SetIfPresent(result, "displayVersion", key.GetValue("ReleaseId"));
                        }
                        SetIfPresent(result, "build", key.GetValue("CurrentBuildNumber"));
                        object ubr = key.GetValue("UBR");
                        if (ubr != null)
                        {
                            int numeric;
                            if (Int32.TryParse(Convert.ToString(ubr, CultureInfo.InvariantCulture), out numeric))
                            {
                                result["ubr"] = numeric;
                            }
                        }
                    }
                }
            }
            catch (System.Security.SecurityException)
            {
                // Registry restrictions should not prevent basic system information.
            }
            catch (UnauthorizedAccessException)
            {
                // Registry restrictions should not prevent basic system information.
            }

            return result;
        }

        private static void SetIfPresent(
            IDictionary<string, object> target,
            string key,
            object value)
        {
            if (value != null)
            {
                target[key] = Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static string FormatDuration(TimeSpan duration)
        {
            int days = Math.Max(0, duration.Days);
            return String.Format(
                CultureInfo.InvariantCulture,
                "{0}.{1:00}:{2:00}:{3:00}",
                days,
                duration.Hours,
                duration.Minutes,
                duration.Seconds);
        }
    }
}
