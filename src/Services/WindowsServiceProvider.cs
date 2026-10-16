using System;
using System.Collections.Generic;
using System.Globalization;
using System.ServiceProcess;

namespace Slate.Services
{
    public sealed class WindowsServiceProvider
    {
        public IDictionary<string, object> ListServices(int limit, string filter)
        {
            List<IDictionary<string, object>> items = new List<IDictionary<string, object>>();
            int total = 0;
            ServiceController[] services = ServiceController.GetServices();
            total = services.Length;

            try
            {
                Array.Sort(services, CompareServicesByName);
                int index;
                for (index = 0; index < services.Length && items.Count < limit; index++)
                {
                    ServiceController service = services[index];
                    IDictionary<string, object> item = Describe(service);
                    string name = Convert.ToString(item["name"], CultureInfo.InvariantCulture);
                    string displayName = Convert.ToString(item["displayName"], CultureInfo.InvariantCulture);
                    if (!String.IsNullOrEmpty(filter) &&
                        name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        displayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    items.Add(item);
                }
            }
            finally
            {
                DisposeAll(services);
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            result["total"] = total;
            result["returned"] = items.Count;
            result["limit"] = limit;
            result["filter"] = filter;
            result["services"] = items;
            return result;
        }

        public IDictionary<string, object> GetService(string serviceName)
        {
            if (String.IsNullOrWhiteSpace(serviceName))
            {
                return null;
            }

            ServiceController service = new ServiceController(serviceName);
            using (service)
            {
                try
                {
                    ServiceControllerStatus status = service.Status;
                    Dictionary<string, object> result = new Dictionary<string, object>();
                    result["name"] = service.ServiceName;
                    result["displayName"] = service.DisplayName;
                    result["status"] = status.ToString();
                    result["serviceType"] = service.ServiceType.ToString();
                    result["canStop"] = SafeCanStop(service);
                    return result;
                }
                catch (InvalidOperationException)
                {
                    return null;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    return null;
                }
                catch (System.Security.SecurityException)
                {
                    return null;
                }
            }
        }

        public IDictionary<string, object> GetServiceCounts()
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            int total = 0;
            int running = 0;
            int stopped = 0;
            int other = 0;
            ServiceController[] services = ServiceController.GetServices();
            try
            {
                int index;
                for (index = 0; index < services.Length; index++)
                {
                    total++;
                    try
                    {
                        ServiceControllerStatus status = services[index].Status;
                        if (status == ServiceControllerStatus.Running)
                        {
                            running++;
                        }
                        else if (status == ServiceControllerStatus.Stopped)
                        {
                            stopped++;
                        }
                        else
                        {
                            other++;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        other++;
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                        other++;
                    }
                }
            }
            finally
            {
                DisposeAll(services);
            }

            result["total"] = total;
            result["running"] = running;
            result["stopped"] = stopped;
            result["otherOrUnavailable"] = other;
            return result;
        }

        private static IDictionary<string, object> Describe(ServiceController service)
        {
            Dictionary<string, object> item = new Dictionary<string, object>();
            item["name"] = SafeReadString(delegate { return service.ServiceName; });
            item["displayName"] = SafeReadString(delegate { return service.DisplayName; });
            item["status"] = SafeReadStatus(service);
            item["serviceType"] = SafeReadServiceType(service);
            item["canStop"] = SafeCanStop(service);
            return item;
        }

        private static int CompareServicesByName(ServiceController left, ServiceController right)
        {
            string leftName = SafeReadString(delegate { return left.ServiceName; });
            string rightName = SafeReadString(delegate { return right.ServiceName; });
            return String.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeReadStatus(ServiceController service)
        {
            try
            {
                return service.Status.ToString();
            }
            catch (InvalidOperationException)
            {
                return "Unavailable";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return "Unavailable";
            }
            catch (System.Security.SecurityException)
            {
                return "Unavailable";
            }
        }

        private static string SafeReadServiceType(ServiceController service)
        {
            try
            {
                return service.ServiceType.ToString();
            }
            catch (InvalidOperationException)
            {
                return "Unavailable";
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return "Unavailable";
            }
            catch (System.Security.SecurityException)
            {
                return "Unavailable";
            }
        }

        private static bool SafeCanStop(ServiceController service)
        {
            try
            {
                return service.CanStop;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }

        private static string SafeReadString(Func<string> reader)
        {
            try
            {
                return reader();
            }
            catch (InvalidOperationException)
            {
                return String.Empty;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                return String.Empty;
            }
        }

        private static void DisposeAll(ServiceController[] services)
        {
            if (services == null)
            {
                return;
            }

            int index;
            for (index = 0; index < services.Length; index++)
            {
                if (services[index] != null)
                {
                    services[index].Dispose();
                }
            }
        }
    }
}
