using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Test
{
    public class WebHostFixture : IDisposable
    {

        IHost _WebHost;

        public WebHostFixture()
        {
            _WebHost = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>()
                    .UseKestrel(options =>
                    {
                        options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                        options.Listen(IPAddress.Loopback, Common._Port);
                    });
                }).Build();
            _WebHost.RunAsync();
            Task.Delay(1000);
        }

        public void Dispose()
        {
            if (_WebHost == null)
                return;
            _WebHost.StopAsync();
        }
    }
    [CollectionDefinition("WebHost collection")]
    public class WebHostCollection : ICollectionFixture<WebHostFixture>
    {
    }
}
