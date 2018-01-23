using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNet.OData.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using maskx.OData;

namespace Test
{
    class Startup
    {
        //http://www.odata.org/getting-started/basic-tutorial/
        //https://www.nuget.org/packages/Microsoft.AspNetCore.OData/
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOData();
            services.AddMvc();
        }
        public void Configure(IApplicationBuilder app)
        {
            var ds = new maskx.OData.Sql.SQL2012("ds","Data Source=.;Initial Catalog=Group;Integrated Security=True");
            app.UseMvc(routeBuilder => {
                routeBuilder.MapDynamicODataServiceRoute("odata",Common._RouterPrefix,ds);
            });
        }
    }
}
