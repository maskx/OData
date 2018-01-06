using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Xunit;

namespace Test
{
    public class WebHostFixture : IDisposable
    {

        IWebHost _WebHost;

        public WebHostFixture()
        {
            _WebHost = WebHost.CreateDefaultBuilder()
                .UseStartup<Startup>()
                .UseKestrel(options =>
                {
                    options.Limits.RequestHeadersTimeout = new TimeSpan(9999999999);
                    options.Listen(IPAddress.Loopback, Common._Port);
                })
                .Build();
            _WebHost.Start();
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
