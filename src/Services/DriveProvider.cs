using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace Slate.Services
{
    public sealed class DriveProvider
    {
        public IDictionary<string, object> ListDrives()
        {
            List<IDictionary<string, object>> drives = new List<IDictionary<string, object>>();
            DriveInfo[] available = DriveInfo.GetDrives();
            int index;
            for (index = 0; index < available.Length; index++)
            {
                drives.Add(DescribeDrive(available[index]));
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            result["total"] = drives.Count;
            result["drives"] = drives;
            return result;
        }

        public int GetReadyDriveCount()
        {
            DriveInfo[] available = DriveInfo.GetDrives();
            int count = 0;
            int index;
            for (index = 0; index < available.Length; index++)
            {
                try
                {
                    if (available[index].IsReady)
                    {
                        count++;
                    }
                }
                catch (IOException)
                {
                    // A removable drive may disappear during inspection.
                }
                catch (UnauthorizedAccessException)
                {
                    // Access to a drive may be restricted.
                }
            }
            return count;
        }

        private static IDictionary<string, object> DescribeDrive(DriveInfo drive)
        {
            Dictionary<string, object> item = new Dictionary<string, object>();
            item["name"] = drive.Name;
            item["driveType"] = drive.DriveType.ToString();
            item["format"] = null;
            item["volumeLabel"] = null;
            item["isReady"] = false;
            item["totalBytes"] = null;
            item["availableBytes"] = null;
            item["usedBytes"] = null;
            item["usedPercent"] = null;

            try
            {
                if (!drive.IsReady)
                {
                    return item;
                }

                long total = drive.TotalSize;
                long available = drive.AvailableFreeSpace;
                long used = Math.Max(0L, total - available);
                item["format"] = drive.DriveFormat;
                item["volumeLabel"] = drive.VolumeLabel;
                item["isReady"] = true;
                item["totalBytes"] = total;
                item["availableBytes"] = available;
                item["usedBytes"] = used;
                item["usedPercent"] = total > 0
                    ? Math.Round((used * 100.0) / total, 2)
                    : 0.0;
            }
            catch (IOException)
            {
                item["error"] = "drive_unavailable";
            }
            catch (UnauthorizedAccessException)
            {
                item["error"] = "access_denied";
            }
            catch (System.Security.SecurityException)
            {
                item["error"] = "access_denied";
            }

            return item;
        }
    }
}
