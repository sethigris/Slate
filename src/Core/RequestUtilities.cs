using System;
using System.Globalization;
using System.Collections.Specialized;

namespace Slate.Core
{
    public static class RequestUtilities
    {
        public static bool TryGetLimit(
            NameValueCollection query,
            string parameterName,
            int defaultValue,
            int maximum,
            out int limit,
            out string error)
        {
            limit = defaultValue;
            error = null;
            if (query == null)
            {
                error = "A query string is required.";
                return false;
            }

            string raw = query[parameterName];
            if (String.IsNullOrEmpty(raw))
            {
                return true;
            }

            int parsed;
            if (!Int32.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out parsed))
            {
                error = "The '" + parameterName + "' parameter must be a positive integer.";
                return false;
            }

            if (parsed < 1 || parsed > maximum)
            {
                error = "The '" + parameterName + "' parameter must be between 1 and " +
                    maximum.ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }

            limit = parsed;
            return true;
        }

        public static string GetSafeFilter(string value, int maximumLength)
        {
            if (String.IsNullOrWhiteSpace(value))
            {
                return String.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.Length > maximumLength)
            {
                return trimmed.Substring(0, maximumLength);
            }

            return trimmed;
        }

        public static bool TryParseProcessId(string value, out int processId)
        {
            return Int32.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out processId) && processId > 0;
        }
    }
}
