using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Slate.Serialization;

namespace Slate.Configuration
{
    public sealed class AppConfig
    {
        public int Port { get; set; }
        public string ApiKey { get; set; }
        public string LogFile { get; set; }
        public int RateLimitPerMinute { get; set; }
        public int MaxConcurrentRequests { get; set; }
        public int MaxRequestBodyBytes { get; set; }
        public int MaxProcessResults { get; set; }
        public int MaxServiceResults { get; set; }
        public bool IncludeServiceDetailsInDiagnostics { get; set; }

        public AppConfig()
        {
            Port = 8765;
            ApiKey = String.Empty;
            LogFile = "slate.log";
            RateLimitPerMinute = 120;
            MaxConcurrentRequests = 32;
            MaxRequestBodyBytes = 16384;
            MaxProcessResults = 250;
            MaxServiceResults = 500;
            IncludeServiceDetailsInDiagnostics = false;
        }

        public string ListenPrefix
        {
            get
            {
                return String.Format(
                    CultureInfo.InvariantCulture,
                    "http://127.0.0.1:{0}/",
                    Port);
            }
        }

        public void Validate()
        {
            if (Port < 1024 || Port > 65535)
            {
                throw new InvalidDataException("Port must be between 1024 and 65535.");
            }

            if (String.IsNullOrWhiteSpace(ApiKey) || ApiKey.Length < 32)
            {
                throw new InvalidDataException(
                    "ApiKey must contain at least 32 characters. Run without a config file to generate one.");
            }

            if (RateLimitPerMinute < 1 || RateLimitPerMinute > 10000)
            {
                throw new InvalidDataException("RateLimitPerMinute must be between 1 and 10000.");
            }

            if (MaxConcurrentRequests < 1 || MaxConcurrentRequests > 256)
            {
                throw new InvalidDataException("MaxConcurrentRequests must be between 1 and 256.");
            }

            if (MaxRequestBodyBytes < 1024 || MaxRequestBodyBytes > 1048576)
            {
                throw new InvalidDataException("MaxRequestBodyBytes must be between 1024 and 1048576.");
            }

            if (MaxProcessResults < 1 || MaxProcessResults > 1000)
            {
                throw new InvalidDataException("MaxProcessResults must be between 1 and 1000.");
            }

            if (MaxServiceResults < 1 || MaxServiceResults > 2000)
            {
                throw new InvalidDataException("MaxServiceResults must be between 1 and 2000.");
            }

            if (String.IsNullOrWhiteSpace(LogFile))
            {
                throw new InvalidDataException("LogFile cannot be empty.");
            }
        }

        public static AppConfig CreateDefaults()
        {
            AppConfig config = new AppConfig();
            config.ApiKey = ApiKeyFactory.Create();
            config.Validate();
            return config;
        }
    }

    public static class ApiKeyFactory
    {
        public static string Create()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            int index;
            for (index = 0; index < bytes.Length; index++)
            {
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    public static class ConfigManager
    {
        public static AppConfig LoadOrCreate(string path, out bool created)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A configuration path is required.", "path");
            }

            string fullPath = Path.GetFullPath(path);
            created = false;

            if (!File.Exists(fullPath))
            {
                AppConfig initial = AppConfig.CreateDefaults();
                Save(fullPath, initial);
                created = true;
                return initial;
            }

            string contents = File.ReadAllText(fullPath, Encoding.UTF8);
            AppConfig config;
            try
            {
                config = JsonUtil.Deserialize<AppConfig>(contents);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(
                    "The configuration file is not valid JSON or does not match the expected structure.",
                    exception);
            }

            if (config == null)
            {
                throw new InvalidDataException("The configuration file contained an empty value.");
            }

            if (String.IsNullOrWhiteSpace(config.ApiKey))
            {
                config.ApiKey = ApiKeyFactory.Create();
                Save(fullPath, config);
                created = true;
            }

            config.Validate();
            return config;
        }

        public static void Save(string path, AppConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }

            config.Validate();
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtil.Serialize(config);
            File.WriteAllText(fullPath, json + Environment.NewLine, new UTF8Encoding(false));
        }
    }
}
