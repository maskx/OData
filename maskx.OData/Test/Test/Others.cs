using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net;
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
                var rtv1 = Common.Get("$metadata", "db1/schemaA", Common._Port_SchemaInUri);
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
          
        }
        public void Configure(IApplicationBuilder app)
        {
           
        }
    }

    public class SchemaInUriStartup
    {
        public void ConfigureServices(IServiceCollection services)
        {
          
        }
        public void Configure(IApplicationBuilder app)
        {
           
        }
    }


}
