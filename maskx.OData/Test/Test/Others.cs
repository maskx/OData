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
            var _WebHost = WebHost.CreateDefaultBuilder()
                 .UseStartup<TwoDatasourceStartup>()
                 .UseKestrel(options =>
                 {
                     options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                     options.Listen(IPAddress.Loopback, Common._Port_TwoDatasource);
                 })
                 .Build();
            try
            {
                _WebHost.Start();
                Common.Get("AspNetUsers", "db1", Common._Port_TwoDatasource);
                Common.Get("Menu", "db2", Common._Port_TwoDatasource);
            }
            finally
            {
                if (_WebHost != null)
                    _WebHost.StopAsync();
            }
        }
        [Fact]
        public void SchemaInUri()
        {
            var _WebHost = WebHost.CreateDefaultBuilder()
                             .UseStartup<SchemaInUriStartup>()
                             .UseKestrel(options =>
                             {
                                 options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                                 options.Listen(IPAddress.Loopback, Common._Port_SchemaInUri);
                             })
                             .Build();
            try
            {
                _WebHost.Start();
             //   var rtv1 = Common.Get("$metadata", "db1/schemaA", Common._Port_SchemaInUri);
                var rtv = Common.Get("Group", "db1/schemaA", Common._Port_SchemaInUri);
                Assert.Equal(HttpStatusCode.OK, rtv.Item1);
                rtv = Common.Get("Group", "db1/schemaB", Common._Port_SchemaInUri);
                Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            }
            finally
            {
                if (_WebHost != null)
                    _WebHost.StopAsync();
            }
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

    public class SchemaInUriStartup
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
                var sourceA = new maskx.OData.Sql.SQL2012("odata", "Data Source =.; Initial Catalog = Group; Integrated Security = True");
                sourceA.Configuration.DefaultSchema = "schemaA";
                routeBuilder.MapDynamicODataServiceRoute("odata1", "db1/SchemaA", sourceA);
            });
            app.UseMvc(routeBuilder =>
            {
                var sourceB = new maskx.OData.Sql.SQL2012("odata", "Data Source=.;Initial Catalog=Group;Integrated Security=True");
                sourceB.Configuration.DefaultSchema = "schemaB";
                routeBuilder.MapDynamicODataServiceRoute("odata2", "db1/SchemaB", sourceB);
            });
        }
    }


}
