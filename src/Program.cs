using System;
using System.Globalization;
using System.Net;
using System.Threading;
using Slate.Configuration;
using Slate.Core;
using Slate.Infrastructure;
using Slate.Testing;
#if !SLATE_CONSOLE
using System.Windows.Forms;
using Slate.UI;
#endif

namespace Slate
{
    internal static class Program
    {
        public const string Version = "1.1.0";
        private const string DefaultConfigPath = "slate.config.json";

#if SLATE_CONSOLE
        private static int Main(string[] args)
        {
            string configPath = DefaultConfigPath;
            bool showKey = false;
            bool showHelp = false;
            bool selfTest = false;
            int index;

            for (index = 0; index < args.Length; index++)
            {
                string argument = args[index];
                if (String.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(argument, "/?", StringComparison.OrdinalIgnoreCase))
                {
                    showHelp = true;
                }
                else if (String.Equals(argument, "--version", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Slate " + Version);
                    return 0;
                }
                else if (String.Equals(argument, "--self-test", StringComparison.OrdinalIgnoreCase))
                {
                    selfTest = true;
                }
                else if (String.Equals(argument, "--show-key", StringComparison.OrdinalIgnoreCase))
                {
                    showKey = true;
                }
                else if (String.Equals(argument, "--config", StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("Missing path after --config.");
                        return 2;
                    }
                    index++;
                    configPath = args[index];
                }
                else
                {
                    Console.Error.WriteLine("Unknown argument: " + argument);
                    PrintUsage();
                    return 2;
                }
            }

            if (showHelp)
            {
                PrintUsage();
                return 0;
            }

            if (selfTest)
            {
                return SelfTests.Run();
            }

            AppConfig config;
            bool configCreated;
            try
            {
                config = ConfigManager.LoadOrCreate(configPath, out configCreated);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Configuration error: " + exception.Message);
                return 2;
            }

            if (showKey)
            {
                Console.WriteLine(config.ApiKey);
                return 0;
            }

            if (configCreated)
            {
                Console.WriteLine("Created configuration: " + System.IO.Path.GetFullPath(configPath));
                Console.WriteLine("Your new Slate API key is shown below. Store it privately:");
                Console.WriteLine(config.ApiKey);
                Console.WriteLine();
            }

            using (FileLogger logger = new FileLogger(config.LogFile))
            using (ApiServer server = new ApiServer(config, logger))
            using (ManualResetEvent stopEvent = new ManualResetEvent(false))
            {
                ConsoleCancelEventHandler cancelHandler = delegate(object sender, ConsoleCancelEventArgs eventArgs)
                {
                    eventArgs.Cancel = true;
                    stopEvent.Set();
                };

                Console.CancelKeyPress += cancelHandler;
                try
                {
                    server.Start();
                    Console.WriteLine("Slate " + Version + " is running.");
                    Console.WriteLine("Local API: " + config.ListenPrefix + "api/v1/");
                    Console.WriteLine("Config:    " + System.IO.Path.GetFullPath(configPath));
                    Console.WriteLine("Log:       " + System.IO.Path.GetFullPath(config.LogFile));
                    Console.WriteLine("Press Ctrl+C to stop.");
                    stopEvent.WaitOne();
                    return 0;
                }
                catch (HttpListenerException exception)
                {
                    logger.Error("Could not start the HTTP listener.", exception);
                    Console.Error.WriteLine("Could not start the HTTP listener: " + exception.Message);
                    Console.Error.WriteLine("If Windows reports Access is denied, run scripts\\setup-urlacl.bat as Administrator once.");
                    Console.Error.WriteLine("If the port is already in use, change Port in the config file.");
                    return 1;
                }
                catch (Exception exception)
                {
                    logger.Error("Slate stopped after a startup error.", exception);
                    Console.Error.WriteLine("Slate failed: " + exception.Message);
                    return 1;
                }
                finally
                {
                    Console.CancelKeyPress -= cancelHandler;
                    server.Stop();
                }
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Slate " + Version + " - local Windows system information API");
            Console.WriteLine("Usage: Slate.Tests.exe --self-test");
        }
#else
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
#endif
    }
}
