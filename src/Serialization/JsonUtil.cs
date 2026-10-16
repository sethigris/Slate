using System;
using System.Web.Script.Serialization;

namespace Slate.Serialization
{
    public static class JsonUtil
    {
        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = 8 * 1024 * 1024;
            serializer.RecursionLimit = 64;
            return serializer;
        }

        public static string Serialize(object value)
        {
            return CreateSerializer().Serialize(value);
        }

        public static T Deserialize<T>(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException("json");
            }

            return CreateSerializer().Deserialize<T>(json);
        }
    }
}
