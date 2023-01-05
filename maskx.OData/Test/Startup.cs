using maskx.OData;
using maskx.OData.Extensions;
using maskx.OData.SQLSource;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;

namespace Test
{
    class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddOData(opt => opt.Count().Filter().Expand().Select().OrderBy().SetMaxTop(5));
            services.AddDynamicOdata();
            services.AddOptions<DynamicODataOptions>().Configure((options) =>
            {

            });
        }
        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseODataRouteDebug();
            app.UseDynamicOdata((builder) =>
            {
                builder.AddDataSource(Common._RouterPrefix, new SQLServer("Data Source=.;Initial Catalog=Northwind;Integrated Security=True"));
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

    }
}
