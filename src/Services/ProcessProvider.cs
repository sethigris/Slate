using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace Slate.Services
{
    public sealed class ProcessProvider
    {
        public IDictionary<string, object> ListProcesses(int limit, string filter)
        {
            List<IDictionary<string, object>> items = new List<IDictionary<string, object>>();
            int total = 0;
            Process[] processes = Process.GetProcesses();
            try
            {
                total = processes.Length;
                Array.Sort(processes, CompareProcessesById);

                int index;
                for (index = 0; index < processes.Length && items.Count < limit; index++)
                {
                    Process process = processes[index];
                    IDictionary<string, object> item = TryGetSummary(process);
                    if (item == null)
                    {
                        continue;
                    }

                    string name = Convert.ToString(item["name"], CultureInfo.InvariantCulture);
                    if (!String.IsNullOrEmpty(filter) &&
                        name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    items.Add(item);
                }
            }
            finally
            {
                int index;
                for (index = 0; index < processes.Length; index++)
                {
                    processes[index].Dispose();
                }
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            result["total"] = total;
            result["returned"] = items.Count;
            result["limit"] = limit;
            result["filter"] = filter;
            result["processes"] = items;
            return result;
        }

        public IDictionary<string, object> GetProcess(int processId)
        {
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }

            using (process)
            {
                Dictionary<string, object> result = new Dictionary<string, object>();
                result["id"] = SafeReadInt(delegate { return process.Id; });
                result["name"] = SafeReadString(delegate { return process.ProcessName; });
                result["workingSetBytes"] = SafeReadLong(delegate { return process.WorkingSet64; });
                result["privateMemoryBytes"] = SafeReadLong(delegate { return process.PrivateMemorySize64; });
                result["threadCount"] = SafeReadInt(delegate { return process.Threads.Count; });
                result["handleCount"] = SafeReadInt(delegate { return process.HandleCount; });
                result["startTimeUtc"] = SafeReadStartTime(process);
                result["responding"] = SafeReadBool(delegate { return process.Responding; });
                return result;
            }
        }

        public int GetTotalProcessCount()
        {
            Process[] processes = Process.GetProcesses();
            int count = processes.Length;
            int index;
            for (index = 0; index < processes.Length; index++)
            {
                processes[index].Dispose();
            }
            return count;
        }

        private static IDictionary<string, object> TryGetSummary(Process process)
        {
            try
            {
                Dictionary<string, object> item = new Dictionary<string, object>();
                item["id"] = process.Id;
                item["name"] = process.ProcessName;
                item["workingSetBytes"] = SafeReadLong(delegate { return process.WorkingSet64; });
                item["privateMemoryBytes"] = SafeReadLong(delegate { return process.PrivateMemorySize64; });
                item["threadCount"] = SafeReadInt(delegate { return process.Threads.Count; });
                item["startTimeUtc"] = SafeReadStartTime(process);
                return item;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }

        private static int CompareProcessesById(Process left, Process right)
        {
            int leftId = SafeReadInt(delegate { return left.Id; });
            int rightId = SafeReadInt(delegate { return right.Id; });
            return leftId.CompareTo(rightId);
        }

        private static object SafeReadStartTime(Process process)
        {
            try
            {
                return process.StartTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }

        private static int SafeReadInt(Func<int> reader)
        {
            try
            {
                return reader();
            }
            catch (Exception exception)
            {
                if (IsReadFailure(exception))
                {
                    return 0;
                }
                throw;
            }
        }

        private static long SafeReadLong(Func<long> reader)
        {
            try
            {
                return reader();
            }
            catch (Exception exception)
            {
                if (IsReadFailure(exception))
                {
                    return 0L;
                }
                throw;
            }
        }

        private static string SafeReadString(Func<string> reader)
        {
            try
            {
                return reader();
            }
            catch (Exception exception)
            {
                if (IsReadFailure(exception))
                {
                    return String.Empty;
                }
                throw;
            }
        }

        private static bool SafeReadBool(Func<bool> reader)
        {
            try
            {
                return reader();
            }
            catch (Exception exception)
            {
                if (IsReadFailure(exception))
                {
                    return false;
                }
                throw;
            }
        }

        private static bool IsReadFailure(Exception exception)
        {
            return exception is InvalidOperationException ||
                exception is System.ComponentModel.Win32Exception ||
                exception is NotSupportedException ||
                exception is System.Security.SecurityException;
        }
    }
}
