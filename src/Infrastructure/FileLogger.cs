using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Slate.Infrastructure
{
    public sealed class FileLogger : IDisposable
    {
        private readonly object _sync = new object();
        private readonly string _path;
        private readonly long _maxBytes;
        private bool _disposed;

        public FileLogger(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A log path is required.", "path");
            }

            _path = Path.GetFullPath(path);
            _maxBytes = 1024L * 1024L;
            string directory = Path.GetDirectoryName(_path);
            if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public void Info(string message)
        {
            Write("INFO", message, null);
        }

        public void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public void Error(string message, Exception exception)
        {
            Write("ERROR", message, exception);
        }

        private void Write(string level, string message, Exception exception)
        {
            if (_disposed)
            {
                return;
            }

            string safeMessage = Sanitize(message);
            StringBuilder line = new StringBuilder();
            line.Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            line.Append(" [");
            line.Append(level);
            line.Append("] ");
            line.Append(safeMessage);

            if (exception != null)
            {
                line.Append(" | ");
                line.Append(Sanitize(exception.GetType().Name + ": " + exception.Message));
            }

            line.Append(Environment.NewLine);

            lock (_sync)
            {
                try
                {
                    RotateIfNeeded();
                    File.AppendAllText(_path, line.ToString(), new UTF8Encoding(false));
                }
                catch (IOException)
                {
                    // Logging must not bring down the API when the disk is unavailable.
                }
                catch (UnauthorizedAccessException)
                {
                    // Logging must not bring down the API when the path is not writable.
                }
            }
        }

        private void RotateIfNeeded()
        {
            FileInfo info = new FileInfo(_path);
            if (!info.Exists || info.Length < _maxBytes)
            {
                return;
            }

            string backup = _path + ".1";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(_path, backup);
        }

        private static string Sanitize(string value)
        {
            if (value == null)
            {
                return String.Empty;
            }

            return value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _disposed = true;
            }
        }
    }
}
