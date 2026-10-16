using System;
using System.Collections.Specialized;
using Slate.Configuration;
using Slate.Core;
using Slate.Security;
using Slate.Serialization;

namespace Slate.Testing
{
    public static class SelfTests
    {
        private static int _passed;
        private static int _failed;

        public static int Run()
        {
            _passed = 0;
            _failed = 0;
            Console.WriteLine("Slate self-tests");
            Console.WriteLine("--------------------");

            RunCase("default configuration validates", TestDefaultConfiguration);
            RunCase("API key contains 256 bits of hex output", TestApiKeyFactory);
            RunCase("Bearer token parsing", TestBearerTokenParsing);
            RunCase("constant-time comparison results", TestConstantTimeComparison);
            RunCase("rate limiter window", TestRateLimiter);
            RunCase("limit query validation", TestLimitQuery);
            RunCase("process ID validation", TestProcessIdParsing);
            RunCase("configuration JSON round-trip", TestConfigRoundTrip);
            RunCase("JSON error envelope", TestErrorEnvelopeJson);

            Console.WriteLine();
            Console.WriteLine("Passed: " + _passed + "  Failed: " + _failed);
            return _failed == 0 ? 0 : 1;
        }

        private static void RunCase(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("PASS  " + name);
            }
            catch (Exception exception)
            {
                _failed++;
                Console.WriteLine("FAIL  " + name + " - " + exception.Message);
            }
        }

        private static void TestDefaultConfiguration()
        {
            AppConfig config = AppConfig.CreateDefaults();
            config.Validate();
            Assert(config.Port == 8765, "Default port should be 8765.");
            Assert(config.ListenPrefix == "http://127.0.0.1:8765/", "Listen prefix should be loopback-only.");
            Assert(config.ApiKey.Length >= 32, "An API key should be generated.");
        }

        private static void TestApiKeyFactory()
        {
            string key = ApiKeyFactory.Create();
            Assert(key.Length == 64, "A 32-byte key should be represented by 64 hex characters.");
            int index;
            for (index = 0; index < key.Length; index++)
            {
                char value = key[index];
                bool valid = (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f');
                Assert(valid, "API key must use lowercase hexadecimal characters.");
            }
        }

        private static void TestBearerTokenParsing()
        {
            string token;
            Assert(ApiKeyAuthenticator.TryReadBearerToken("Bearer abc123", out token), "Bearer token should parse.");
            Assert(token == "abc123", "Parsed token should match.");
            Assert(ApiKeyAuthenticator.TryReadBearerToken("bEaReR abc123", out token), "Scheme should be case-insensitive.");
            Assert(!ApiKeyAuthenticator.TryReadBearerToken("Basic abc123", out token), "Basic auth should be rejected.");
            Assert(!ApiKeyAuthenticator.TryReadBearerToken("Bearer ", out token), "An empty token should be rejected.");
            Assert(!ApiKeyAuthenticator.TryReadBearerToken(null, out token), "A missing header should be rejected.");
        }

        private static void TestConstantTimeComparison()
        {
            Assert(ApiKeyAuthenticator.ConstantTimeEquals("aabbcc", "aabbcc"), "Identical values should match.");
            Assert(!ApiKeyAuthenticator.ConstantTimeEquals("aabbcc", "aabbcd"), "Different values should not match.");
            Assert(!ApiKeyAuthenticator.ConstantTimeEquals("short", "shorter"), "Different lengths should not match.");
            Assert(!ApiKeyAuthenticator.ConstantTimeEquals(null, "value"), "Null values should not match.");
        }

        private static void TestRateLimiter()
        {
            RateLimiter limiter = new RateLimiter(2);
            DateTime start = new DateTime(2026, 1, 1, 0, 0, 5, DateTimeKind.Utc);
            Assert(limiter.IsAllowed("127.0.0.1", start), "First request should pass.");
            Assert(limiter.IsAllowed("127.0.0.1", start.AddSeconds(1)), "Second request should pass.");
            Assert(!limiter.IsAllowed("127.0.0.1", start.AddSeconds(2)), "Third request should be limited.");
            Assert(limiter.IsAllowed("127.0.0.2", start.AddSeconds(2)), "A different client should have its own bucket.");
            Assert(limiter.IsAllowed("127.0.0.1", start.AddMinutes(1)), "The next window should reset the counter.");
            Assert(limiter.GetRetryAfterSeconds(start) > 0, "Retry-after should be positive.");
        }

        private static void TestLimitQuery()
        {
            NameValueCollection query = new NameValueCollection();
            int limit;
            string error;
            Assert(RequestUtilities.TryGetLimit(query, "limit", 50, 250, out limit, out error), "An omitted limit should use the default.");
            Assert(limit == 50, "Default limit should be assigned.");
            query["limit"] = "25";
            Assert(RequestUtilities.TryGetLimit(query, "limit", 50, 250, out limit, out error), "A valid number should pass.");
            Assert(limit == 25, "Parsed limit should be 25.");
            query["limit"] = "0";
            Assert(!RequestUtilities.TryGetLimit(query, "limit", 50, 250, out limit, out error), "Zero should be rejected.");
            query["limit"] = "2.5";
            Assert(!RequestUtilities.TryGetLimit(query, "limit", 50, 250, out limit, out error), "A decimal should be rejected.");
            query["limit"] = "999";
            Assert(!RequestUtilities.TryGetLimit(query, "limit", 50, 250, out limit, out error), "Values above the maximum should be rejected.");
        }

        private static void TestProcessIdParsing()
        {
            int id;
            Assert(RequestUtilities.TryParseProcessId("42", out id) && id == 42, "A positive integer should parse.");
            Assert(!RequestUtilities.TryParseProcessId("0", out id), "Zero should not parse as a process ID.");
            Assert(!RequestUtilities.TryParseProcessId("-1", out id), "Negative values should be rejected.");
            Assert(!RequestUtilities.TryParseProcessId("42x", out id), "Trailing letters should be rejected.");
            Assert(!RequestUtilities.TryParseProcessId("", out id), "Empty values should be rejected.");
        }

        private static void TestConfigRoundTrip()
        {
            AppConfig source = AppConfig.CreateDefaults();
            source.Port = 9999;
            string json = JsonUtil.Serialize(source);
            AppConfig copy = JsonUtil.Deserialize<AppConfig>(json);
            Assert(copy != null, "Deserialization should return a config object.");
            Assert(copy.Port == 9999, "Port should survive serialization.");
            Assert(copy.ApiKey == source.ApiKey, "API key should survive serialization.");
            Assert(copy.RateLimitPerMinute == source.RateLimitPerMinute, "Rate limit should survive serialization.");
        }

        private static void TestErrorEnvelopeJson()
        {
            ApiResponse response = ApiResponse.Error(400, "invalid_parameter", "Bad input.");
            string json = JsonUtil.Serialize(response.Body);
            Assert(json.IndexOf("\"success\":false", StringComparison.Ordinal) >= 0, "Error response should report failure.");
            Assert(json.IndexOf("invalid_parameter", StringComparison.Ordinal) >= 0, "Error response should carry its code.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
