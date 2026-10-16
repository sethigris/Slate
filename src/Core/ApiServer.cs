using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using Slate.Configuration;
using Slate.Infrastructure;
using Slate.Security;
using Slate.Serialization;

namespace Slate.Core
{
    public sealed class ApiServer : IDisposable
    {
        private readonly AppConfig _config;
        private readonly FileLogger _logger;
        private readonly ApiKeyAuthenticator _authenticator;
        private readonly RateLimiter _rateLimiter;
        private readonly RouteDispatcher _dispatcher;
        private readonly SemaphoreSlim _slots;
        private readonly object _workerSync = new object();
        private readonly ManualResetEvent _workersDrained = new ManualResetEvent(true);
        private int _activeWorkers;
        private bool _safeToDispose;
        private HttpListener _listener;
        private Thread _acceptThread;
        private int _stopping;

        public ApiServer(AppConfig config, FileLogger logger)
        {
            if (config == null)
            {
                throw new ArgumentNullException("config");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }

            _config = config;
            _logger = logger;
            _authenticator = new ApiKeyAuthenticator(config.ApiKey);
            _rateLimiter = new RateLimiter(config.RateLimitPerMinute);
            _dispatcher = new RouteDispatcher(config);
            _slots = new SemaphoreSlim(config.MaxConcurrentRequests, config.MaxConcurrentRequests);
        }

        public void Start()
        {
            if (_listener != null)
            {
                throw new InvalidOperationException("The server has already been started.");
            }

            HttpListener listener = new HttpListener();
            listener.IgnoreWriteExceptions = true;
            listener.Prefixes.Add(_config.ListenPrefix);
            listener.Start();
            _listener = listener;

            _acceptThread = new Thread(AcceptLoop);
            _acceptThread.IsBackground = true;
            _acceptThread.Name = "Slate HTTP accept loop";
            _acceptThread.Start();
            _logger.Info("HTTP listener started at " + _config.ListenPrefix);
        }

        private void AcceptLoop()
        {
            while (Interlocked.CompareExchange(ref _stopping, 0, 0) == 0)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    if (Interlocked.CompareExchange(ref _stopping, 0, 0) != 0)
                    {
                        break;
                    }
                    _logger.Warn("The HTTP listener reported an error while accepting a request.");
                    continue;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (!_slots.Wait(0))
                {
                    WriteResponse(
                        context,
                        ApiResponse.Error(503, "server_busy", "The server is handling its maximum number of concurrent requests."),
                        Guid.NewGuid().ToString("N"));
                    continue;
                }

                RegisterWorker();
                bool queued = ThreadPool.QueueUserWorkItem(delegate(object state)
                {
                    try
                    {
                        HandleContext((HttpListenerContext)state);
                    }
                    finally
                    {
                        _slots.Release();
                        UnregisterWorker();
                    }
                }, context);

                if (!queued)
                {
                    _slots.Release();
                    UnregisterWorker();
                    WriteResponse(
                        context,
                        ApiResponse.Error(503, "server_busy", "The request could not be queued."),
                        Guid.NewGuid().ToString("N"));
                }
            }
        }

        private void RegisterWorker()
        {
            lock (_workerSync)
            {
                _activeWorkers++;
                _workersDrained.Reset();
            }
        }

        private void UnregisterWorker()
        {
            lock (_workerSync)
            {
                if (_activeWorkers > 0)
                {
                    _activeWorkers--;
                }

                if (_activeWorkers == 0)
                {
                    _workersDrained.Set();
                }
            }
        }

        private void HandleContext(HttpListenerContext context)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string requestId = Guid.NewGuid().ToString("N");
            string path = "/";
            string method = "UNKNOWN";
            int status = 500;

            try
            {
                HttpListenerRequest request = context.Request;
                method = request.HttpMethod;
                if (request.Url != null)
                {
                    path = request.Url.AbsolutePath;
                }

                if (request.RawUrl != null && request.RawUrl.Length > 2048)
                {
                    WriteResponse(context, ApiResponse.Error(414, "uri_too_long", "The request URL is too long."), requestId);
                    status = 414;
                    return;
                }

                if (request.ContentLength64 > _config.MaxRequestBodyBytes)
                {
                    WriteResponse(context, ApiResponse.Error(413, "body_too_large", "Request bodies are not accepted by this read-only API."), requestId);
                    status = 413;
                    return;
                }

                string transferEncoding = request.Headers["Transfer-Encoding"];
                if (request.ContentLength64 > 0 || !String.IsNullOrEmpty(transferEncoding))
                {
                    WriteResponse(context, ApiResponse.Error(400, "body_not_supported", "This read-only API does not accept request bodies."), requestId);
                    status = 400;
                    return;
                }

                string clientKey = GetClientKey(request);
                DateTime now = DateTime.UtcNow;
                if (!_rateLimiter.IsAllowed(clientKey, now))
                {
                    ApiResponse limited = ApiResponse.Error(429, "rate_limit_exceeded", "Too many requests from this client.");
                    limited.AddHeader("Retry-After", _rateLimiter.GetRetryAfterSeconds(now).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    WriteResponse(context, limited, requestId);
                    status = 429;
                    return;
                }

                if (!_authenticator.IsAuthorized(request.Headers["Authorization"]))
                {
                    ApiResponse unauthorized = ApiResponse.Error(401, "unauthorized", "A valid Bearer API key is required.");
                    unauthorized.AddHeader("WWW-Authenticate", "Bearer realm=\"Slate\"");
                    WriteResponse(context, unauthorized, requestId);
                    status = 401;
                    return;
                }

                ApiResponse response = _dispatcher.Dispatch(request);
                status = response.StatusCode;
                WriteResponse(context, response, requestId);
            }
            catch (HttpListenerException exception)
            {
                status = 500;
                _logger.Warn("HTTP request failed for " + path + " (" + exception.GetType().Name + ").");
                TryWriteError(context, 500, "internal_error", "The request could not be completed.", requestId);
            }
            catch (Exception exception)
            {
                status = 500;
                _logger.Error("Unhandled request error for " + path, exception);
                TryWriteError(context, 500, "internal_error", "The request could not be completed.", requestId);
            }
            finally
            {
                stopwatch.Stop();
                _logger.Info(String.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "requestId={0} method={1} path={2} status={3} elapsedMs={4}",
                    requestId,
                    method,
                    path,
                    status,
                    stopwatch.ElapsedMilliseconds));
            }
        }

        private static string GetClientKey(HttpListenerRequest request)
        {
            if (request.RemoteEndPoint == null || request.RemoteEndPoint.Address == null)
            {
                return "unknown";
            }

            return request.RemoteEndPoint.Address.ToString();
        }

        private static void TryWriteError(
            HttpListenerContext context,
            int statusCode,
            string code,
            string message,
            string requestId)
        {
            try
            {
                WriteResponse(context, ApiResponse.Error(statusCode, code, message), requestId);
            }
            catch (Exception)
            {
                try
                {
                    context.Response.Abort();
                }
                catch (Exception)
                {
                }
            }
        }

        private static void WriteResponse(HttpListenerContext context, ApiResponse response, string requestId)
        {
            HttpListenerResponse httpResponse = context.Response;
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtil.Serialize(response.Body));
            httpResponse.StatusCode = response.StatusCode;
            httpResponse.ContentType = "application/json; charset=utf-8";
            httpResponse.ContentEncoding = Encoding.UTF8;
            httpResponse.ContentLength64 = bytes.Length;
            httpResponse.KeepAlive = false;
            httpResponse.AddHeader("Cache-Control", "no-store");
            httpResponse.AddHeader("X-Content-Type-Options", "nosniff");
            httpResponse.AddHeader("X-Request-Id", requestId);

            foreach (System.Collections.Generic.KeyValuePair<string, string> header in response.Headers)
            {
                httpResponse.AddHeader(header.Key, header.Value);
            }

            try
            {
                httpResponse.OutputStream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                httpResponse.Close();
            }
        }

        public void Stop()
        {
            bool wasAlreadyStopping = Interlocked.Exchange(ref _stopping, 1) != 0;
            if (!wasAlreadyStopping)
            {
                try
                {
                    if (_listener != null)
                    {
                        _listener.Stop();
                        _listener.Close();
                    }
                }
                catch (HttpListenerException exception)
                {
                    _logger.Warn("Listener shutdown reported " + exception.GetType().Name + ".");
                }

                if (_acceptThread != null && _acceptThread.IsAlive)
                {
                    _acceptThread.Join(2000);
                }

                _logger.Info("HTTP listener stopped.");
            }

            // Give in-flight read-only inspections time to finish before disposing shared synchronization objects.
            _safeToDispose = _workersDrained.WaitOne(5000);
            if (!_safeToDispose)
            {
                _logger.Warn("Some requests were still active during shutdown; shared worker controls will be left for process cleanup.");
            }
        }

        public void Dispose()
        {
            Stop();
            if (_safeToDispose)
            {
                _slots.Dispose();
                _workersDrained.Close();
            }
        }
    }
}
