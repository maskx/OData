using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using System.Net.Http.Formatting;

namespace Test
{
    class Common
    {
        public static string _RouterPrefix = "ss";
        public static int _Port = 5008;
        public static int _Port_ChangeDefaultSchema = 9001;
        public static int _Port_SchemaInUri = 9002;
        public static int _Port_TwoDatasource = 9003;
        public static int _Port_LowerName = 9004;

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
            HttpResponseMessage response = client.PostAsJsonAsync(string.Format(Tpl, query), content).Result;
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

            HttpResponseMessage response = client.PutAsJsonAsync(string.Format(Tpl, query), content).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, string>(response.StatusCode, str);
        }
        public static ValueTuple<HttpStatusCode, string> Patch(string query, object content)
        {
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(new HttpMethod("PATCH"), string.Format(Tpl, query))
            {
                Content = new ObjectContent(content.GetType(), content, new JsonMediaTypeFormatter())
            };
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, string>(response.StatusCode, str);
        }

        public static ValueTuple<HttpStatusCode, string> Get(string target, string dataSource, int port)
        {
            string tpl = string.Format("http://{0}:{1}/{2}/{3}", IPAddress.Loopback, port, dataSource, target);
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, tpl);
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            return new ValueTuple<HttpStatusCode, string>(response.StatusCode, str);
        }
    }
}
