using System.Collections.Generic;

namespace Slate.Core
{
    public sealed class ApiResponse
    {
        public int StatusCode { get; private set; }
        public object Body { get; private set; }
        public IDictionary<string, string> Headers { get; private set; }

        public ApiResponse(int statusCode, object body)
        {
            StatusCode = statusCode;
            Body = body;
            Headers = new Dictionary<string, string>();
        }

        public ApiResponse AddHeader(string name, string value)
        {
            Headers[name] = value;
            return this;
        }

        public static ApiResponse Ok(object body)
        {
            return new ApiResponse(200, body);
        }

        public static ApiResponse Error(int statusCode, string code, string message)
        {
            Dictionary<string, object> details = new Dictionary<string, object>();
            details["code"] = code;
            details["message"] = message;

            Dictionary<string, object> body = new Dictionary<string, object>();
            body["success"] = false;
            body["error"] = details;
            return new ApiResponse(statusCode, body);
        }
    }
}
