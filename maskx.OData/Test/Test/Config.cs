using maskx.OData;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Net;
using Xunit;

namespace Test
{
    public class Config
    {
        [Fact]
        public void ChangeDefaultSchema()
        {
            var _WebHost = WebHost.CreateDefaultBuilder()
                             .UseStartup<ChangeDefaultSchemaStartup>()
                             .UseKestrel(options =>
                             {
                                 options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                                 options.Listen(IPAddress.Loopback, Common._Port_ChangeDefaultSchema);
                             })
                             .Build();
            try
            {
                _WebHost.Start();

                var rtv = Common.Get("$metadata", "db1", Common._Port_ChangeDefaultSchema);

                rtv = Common.Get("dbo.Tag", "db1", Common._Port_ChangeDefaultSchema);
                Assert.Equal(HttpStatusCode.OK, rtv.Item1);
                var jobj = JObject.Parse(rtv.Item2);
                Assert.EndsWith("#dbo.Tag", jobj.Property("@odata.context").Value.ToString());

                rtv = Common.Get("Group", "db1", Common._Port_ChangeDefaultSchema);
                Assert.Equal(HttpStatusCode.OK, rtv.Item1);
                jobj = JObject.Parse(rtv.Item2);
                Assert.EndsWith("#Group", jobj.Property("@odata.context").Value.ToString());
            }
            finally
            {
                if (_WebHost != null)
                    _WebHost.StopAsync();
            }
        }
        public class ChangeDefaultSchemaStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddRouting();
                services.AddOData();
                services.TryAddEnumerable(ServiceDescriptor.Transient<IODataControllerActionConvention, DynamicODataControllerActionConvention>());
            }
            public void Configure(IApplicationBuilder app)
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            }
        }

        [Fact]
        public void LowerName()
        {
            var _WebHost = WebHost.CreateDefaultBuilder()
                                         .UseStartup<LowerNameStartup>()
                                         .UseKestrel(options =>
                                         {
                                             options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                                             options.Listen(IPAddress.Loopback, Common._Port_LowerName);
                                         })
                                         .Build();
            try
            {
                _WebHost.Start();
                var rtv1 = Common.Get("$metadata", "db1", Common._Port_LowerName);
                var rtv = Common.Get("tag", "db1", Common._Port_LowerName);
                Assert.Equal(HttpStatusCode.OK, rtv.Item1);
            }
            finally
            {
                if (_WebHost != null)
                    _WebHost.StopAsync();
            }
        }
        public class LowerNameStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddRouting();
                services.AddOData();
                services.TryAddEnumerable(ServiceDescriptor.Transient<IODataControllerActionConvention, DynamicODataControllerActionConvention>());
            }
            public void Configure(IApplicationBuilder app)
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                });
            }
        }
    }
}
