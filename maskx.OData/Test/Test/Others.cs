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
        [Fact]
        public void TwoDatasource()
        {
            int port = 8090;
            var _WebHost = WebHost.CreateDefaultBuilder()
                 .UseStartup<TwoDatasourceStartup>()
                 .UseKestrel(options =>
                 {
                     options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                     options.Listen(IPAddress.Loopback, port);
                 })
                 .Build();
            try
            {
                _WebHost.Start();
                Get(port, "db1", "AspNetUsers");
                Get(port, "db2", "Menu");
            }
            finally
            {
                if (_WebHost != null)
                    _WebHost.StopAsync();
            }

        }
        [Fact]
        public void Schema()
        {
            int port = 8091;
            var _WebHost = WebHost.CreateDefaultBuilder()
                             .UseStartup<SchemaStartup>()
                             .UseKestrel(options =>
                             {
                                 options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                                 options.Listen(IPAddress.Loopback, port);
                             })
                             .Build();
            try
            {
                _WebHost.Start();
                Get(port, "db1/schemaA", "schemaA.Group");
                Get(port, "db1/schemaB", "schemaB.Group");
            }
            finally
            {
                if (_WebHost != null)
                    _WebHost.StopAsync();
            }

        }

        [Fact]
        public void ChangeDefaultSchema()
        {
            int port = 8092;
            var _WebHost = WebHost.CreateDefaultBuilder()
                             .UseStartup<ChangeDefaultSchemaStartup>()
                             .UseKestrel(options =>
                             {
                                 options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                                 options.Listen(IPAddress.Loopback, port);
                             })
                             .Build();
            try
            {
                _WebHost.Start();
               // Get(port, "db1", "$metadata");
                Get(port, "db1", "dbo.Tag");
                Get(port, "db1", "Group");
            }
            finally
            {
                if (_WebHost != null)
                    _WebHost.StopAsync();
            }
        }
        public void Get(int port, string dataSource, string target)
        {
            string tpl = string.Format("http://{0}:{1}/{2}/{3}", IPAddress.Loopback, port, dataSource, target);
            HttpClient client = new HttpClient();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, tpl);
            HttpResponseMessage response = client.SendAsync(request).Result;
            var str = response.Content.ReadAsStringAsync().Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var rtv = JObject.Parse(str);
            Assert.Equal(2, rtv.Count);
            Assert.EndsWith("$metadata#" + target, rtv.Property("@odata.context").Value.ToString());
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
            app.UseMvc(routeBuilder =>
            {
                routeBuilder.MapDynamicODataServiceRoute("odata1", "db1",
                    new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True"));
            });
            app.UseMvc(routeBuilder =>
            {
                routeBuilder.MapDynamicODataServiceRoute("odata2", "db2",
                    new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=test;Integrated Security=True"));
            });
        }
    }

    public class SchemaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOData();
            services.AddMvc();
        }
        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc(routeBuilder =>
            {
                routeBuilder.MapDynamicODataServiceRoute("odata1", "db1/SchemaA",
                    new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True"));
            });
            app.UseMvc(routeBuilder =>
            {
                routeBuilder.MapDynamicODataServiceRoute("odata2", "db1/SchemaB",
                    new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True"));
            });
        }
    }

    public class ChangeDefaultSchemaStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOData();
            services.AddMvc();
        }
        public void Configure(IApplicationBuilder app)
        {
            app.UseMvc(routeBuilder =>
            {
                var dataSource = new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True");
                dataSource.Configuration.DefaultSchema = "schemaB";
                dataSource.Configuration.LowerName = true;
                routeBuilder.MapDynamicODataServiceRoute("odata1", "db1", dataSource);
            });
        }
    }
}
