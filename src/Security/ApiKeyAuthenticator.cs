using System;

namespace Slate.Security
{
    public sealed class ApiKeyAuthenticator
    {
        private readonly string _expectedKey;

        public ApiKeyAuthenticator(string expectedKey)
        {
            if (String.IsNullOrEmpty(expectedKey))
            {
                throw new ArgumentException("An API key is required.", "expectedKey");
            }

            _expectedKey = expectedKey;
        }

        public bool IsAuthorized(string authorizationHeader)
        {
            string suppliedKey;
            if (!TryReadBearerToken(authorizationHeader, out suppliedKey))
            {
                return false;
            }

            return ConstantTimeEquals(_expectedKey, suppliedKey);
        }

        public static bool TryReadBearerToken(string authorizationHeader, out string token)
        {
            token = null;
            if (String.IsNullOrWhiteSpace(authorizationHeader))
            {
                return false;
            }

            const string prefix = "Bearer ";
            if (authorizationHeader.Length <= prefix.Length ||
                String.Compare(
                    authorizationHeader,
                    0,
                    prefix,
                    0,
                    prefix.Length,
                    StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            string candidate = authorizationHeader.Substring(prefix.Length).Trim();
            if (candidate.Length == 0 || candidate.IndexOf(' ') >= 0 || candidate.IndexOf('\t') >= 0)
            {
                return false;
            }

            token = candidate;
            return true;
        }

        public static bool ConstantTimeEquals(string left, string right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            int difference = left.Length ^ right.Length;
            int maximum = Math.Max(left.Length, right.Length);
            int index;
            for (index = 0; index < maximum; index++)
            {
                char first = index < left.Length ? left[index] : (char)0;
                char second = index < right.Length ? right[index] : (char)0;
                difference |= first ^ second;
            }

            return difference == 0;
        }
    }
}
