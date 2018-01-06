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
        public static int _Port = 5000;
        static Common()
        {
            tpl = string.Format("http://{0}:{1}/{2}/{{0}}", IPAddress.Loopback, _Port, _RouterPrefix);
        }
        public static string tpl
        {
            get; private set;
        }
        public static ValueTuple<HttpStatusCode, JObject> Get(string query)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, string.Format(tpl, query));
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));
        }
        public static ValueTuple<HttpStatusCode, JObject> Post(string query, object content)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.PostAsync(string.Format(tpl, query), content == null ? null : new JsonContent(content)).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));

        }
        public static ValueTuple<HttpStatusCode, JObject> Delete(string query)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, string.Format(tpl, query));
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));
        }
        public static ValueTuple<HttpStatusCode, JObject> Put(string query, object content)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, string.Format(tpl, query));
            request.Content = new JsonContent(content);
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));
        }
        public static ValueTuple<HttpStatusCode, JObject> Patch(string query, object content)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), string.Format(tpl, query));
            request.Content = new JsonContent(content);
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, JObject>(response.StatusCode, JObject.Parse(str));
        }
    }
}
