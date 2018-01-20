using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;


namespace Test
{
    class Common
    {
        public static string _RouterPrefix = "ss";
        public static int _Port = 5008;
        static Common()
        {
            Tpl = string.Format("http://{0}:{1}/{2}/{{0}}", IPAddress.Loopback, _Port, _RouterPrefix);
        }
        public static string Tpl
        {
            get; private set;
        }
        public static ValueTuple<HttpStatusCode, JObject> GetJObject(string query)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, string.Format(Tpl, query));
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));
        }
        public static ValueTuple<HttpStatusCode, string> GetContent(string query)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, string.Format(Tpl, query));
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, string>(response.StatusCode, str);
        }
        public static ValueTuple<HttpStatusCode, JObject> Post(string query, object content)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.PostAsync(string.Format(Tpl, query), content == null ? null : new JsonContent(content)).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));

        }
        public static ValueTuple<HttpStatusCode, JObject> Delete(string query)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, string.Format(Tpl, query));
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));
        }
        public static ValueTuple<HttpStatusCode, string> Put(string query, object content)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, string.Format(Tpl, query))
            {
                Content = new JsonContent(content)
            };
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, string>(response.StatusCode, str);
        }
        public static ValueTuple<HttpStatusCode, string> Patch(string query, object content)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), string.Format(Tpl, query))
            {
                Content = new JsonContent(content)
            };
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, string>(response.StatusCode, str);
        }
    }
}
