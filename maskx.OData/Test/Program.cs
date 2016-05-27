using maskx.OData;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json.Linq;
using Owin;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.OData.Extensions;

namespace Test
{
    class Program
    {
        static string baseUrl = "http://localhost:3338";
        static string _DataSourceName = "ss";
        static void Main(string[] args)
        {

            string tpl = baseUrl + "/odata/{0}/{1}";
            using (WebApp.Start(baseUrl, Configuration))
            {
                // SendQuery(string.Format(tpl, _DataSourceName, string.Empty), "Query service document.").Wait();
                //  SendQuery(string.Format(tpl, _DataSourceName, "$metadata"), "Query $metadata.").Wait();
                // SendQuery(string.Format(tpl, _DataSourceName, "AspNetUsers"), "Query AspNetUsers.").Wait();
                //   SendQuery(string.Format(tpl, _DataSourceName, "AspNetUsers?$expand=AspNetUserRoles"), "Query $expand").Wait();
                // SendQuery(string.Format(tpl, _DataSourceName, "AspNetUsers?$filter=(endswith(UserName,'min')) or (UserName eq null)&$top=1&$skip=1&$orderby=UserName desc"), "Query AspNetUsers.").Wait();
                // SendQuery(string.Format(tpl, _DataSourceName, "GetChildrenOrgs(UserId='1',ParentCode='A0000')"), "GetChildrenOrgs").Wait();
                // BatchRequest();
                // InvokeSP_Post();
                SendQuery(string.Format(tpl, _DataSourceName, "tvfTest(branchNo=1,culture='A0000')"), "tvfTest").Wait();

                //  SendQuery(string.Format(tpl, _DataSourceName, "vContact"), "Query AspNetUsers.").Wait();

            }
            Console.WriteLine("press any key to continue...");
            Console.Read();
        }

        private static void Configuration(IAppBuilder builder)
        {
            HttpConfiguration configuration = new HttpConfiguration();
            var server = new HttpServer(configuration);
            configuration.Routes.MapDynamicODataServiceRoute(
                  "odata",
                  "odata",
                  server);
            DynamicOData.AddDataSource(new maskx.OData.Sql.SQLDataSource(_DataSourceName));
            //DynamicOData.AddDataSource(new maskx.OData.Sql.SQL2008(_DataSourceName,
            //    ConnectionString,
            //    (action, target) =>
            //    {
            //        Console.WriteLine("{0}\t{1}\t{2}", "permissionCheck", action, target);
            //        return true;
            //    }));
            //DynamicOData.BeforeExcute = (ri) =>
            //{
            //    ri.Parameters["UserId"] = new JValue(3003);
            //    Console.WriteLine("BeforeExcute:{0}", ri.Target);
            //};
            configuration.AddODataQueryFilter();
            builder.UseWebApi(configuration);

        }
        static async Task SendQuery(string query, string queryDescription)
        {
            Console.WriteLine("Sending request to: {0}. Executing {1}...", query, queryDescription);

            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, query);
            HttpResponseMessage response = await client.SendAsync(request);

            Console.WriteLine("\r\nResult:");
            Console.WriteLine(response.StatusCode.ToString());
            Console.WriteLine(await response.Content.ReadAsStringAsync());
        }
        static void PostEntity()
        {
            string tpl = baseUrl + "/odata/{0}/{1}";
            var query = string.Format(tpl, _DataSourceName, "Area");
            HttpClient client = new HttpClient();
            HttpResponseMessage response = client.PostAsJsonAsync(query, new { Name = "test", ParentId = 0 }).Result;

            Console.WriteLine("\r\nResult:");
            Console.WriteLine(response.StatusCode.ToString());
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);
        }
        static void BatchRequest()
        {
            string tpl = "http://localhost.fiddler:3333" + "/odata/{0}/{1}";

            HttpClient client = new HttpClient();

            //Create a request to query for customers
            HttpRequestMessage AspNetUsers = new HttpRequestMessage(HttpMethod.Get, string.Format(tpl, _DataSourceName, "AspNetUsers"));
            //Create a message to add a customer
            HttpRequestMessage AspNetRoles = new HttpRequestMessage(HttpMethod.Get, string.Format(tpl, _DataSourceName, "AspNetRoles"));

            string b = Guid.NewGuid().ToString();
            //Create the different parts of the multipart content
            HttpMessageContent queryContent1 = new HttpMessageContent(AspNetUsers);
            HttpMessageContent queryContent2 = new HttpMessageContent(AspNetRoles);

            if (queryContent1.Headers.Contains("Content-Type")) queryContent1.Headers.Remove("Content-Type");
            queryContent1.Headers.Add("Content-Type", "application/http");
            queryContent1.Headers.Add("client-request-id", "1");
            queryContent1.Headers.Add("return-client-request-id", "True");
            queryContent1.Headers.Add("Content-ID", "1");
            queryContent1.Headers.Add("DataServiceVersion", "3.0");
            queryContent1.Headers.Add("Content-Transfer-Encoding", "binary");
            if (queryContent2.Headers.Contains("Content-Type")) queryContent2.Headers.Remove("Content-Type");
            queryContent2.Headers.Add("Content-Type", "application/http");
            queryContent2.Headers.Add("client-request-id", "2");
            queryContent2.Headers.Add("return-client-request-id", "True");
            queryContent2.Headers.Add("Content-ID", "2");
            queryContent2.Headers.Add("DataServiceVersion", "3.0");
            queryContent2.Headers.Add("Content-Transfer-Encoding", "binary");
            //Create the multipart/mixed message content
            MultipartContent content = new MultipartContent("mixed", "batch_" + b);
            content.Add(queryContent1);
            content.Add(queryContent2);


            //Create the request to the batch service
            HttpRequestMessage batchRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:3333" + "/odata/$batch");
            //Associate the content with the message
            batchRequest.Content = content;

            var response = client.SendAsync(batchRequest).Result;
            Console.WriteLine("\r\nResult:");
            Console.WriteLine(response.StatusCode.ToString());
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);
        }
        static void InvokeSP_Post()
        {
            string tpl = baseUrl + "/odata/{0}/{1}";
            var query = string.Format(tpl, _DataSourceName, "GetUserInfo()");
            Console.WriteLine(query);
            HttpClient client = new HttpClient();
            //  HttpResponseMessage response = client.PostAsJsonAsync(serviceUrl + query, new { UserId = 1, ProductId = 2, tb = new { col1=3} }).Result;

            HttpResponseMessage response = client.PostAsJsonAsync(query, new { UserId = 2 }).Result;

            Console.WriteLine("\r\nResult:");
            Console.WriteLine(response.StatusCode.ToString());
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);
        }
    }
}
