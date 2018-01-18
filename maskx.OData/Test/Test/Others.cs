using maskx.OData;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace Test
{
    public class Others
    {
        IWebHost _WebHost;
        int _Port = 3398;
        [Fact]
        public void TwoDatasource()
        {
            _WebHost = WebHost.CreateDefaultBuilder()
                .UseStartup<TwoDatasourceStartup>()
                .UseKestrel(options =>
                {
                    options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                    options.Listen(IPAddress.Loopback, _Port);
                })
                .Build();
            _WebHost.Start();
            Get("db1","AspNetUsers");
            Get("db2", "Menu");
        }
        public void Get(string dataSource,string target)
        {
           string tpl = string.Format("http://{0}:{1}/{2}/{3}", IPAddress.Loopback, _Port, dataSource,target);
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, tpl);
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var rtv = JObject.Parse(str);
            Assert.Equal(2, rtv.Count);
            Assert.EndsWith("$metadata#"+ target, rtv.Property("@odata.context").Value.ToString());
        }
    }
   
    public class TwoDatasourceStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOData();
            services.AddMvc();
        }
        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc(routeBuilder => {
                routeBuilder.MapDynamicODataServiceRoute("odata1", "db1",
                    new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True"));
            });
            app.UseMvc(routeBuilder => {
                routeBuilder.MapDynamicODataServiceRoute("odata2", "db2",
                    new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=test;Integrated Security=True"));
            });
        }
    }
}
